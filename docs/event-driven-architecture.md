# Event-driven communication: UserManagement → ProductCatalog

UserManagement and ProductCatalog otherwise never talk to each other — each owns its own database and is reachable only over its own HTTP API. This document describes the one asynchronous integration between them, via RabbitMQ.

## Flow

```mermaid
sequenceDiagram
    participant Client
    participant UserManagement.API
    participant RabbitMQ
    participant ProductCatalog.API

    Client->>UserManagement.API: POST /auth/register
    UserManagement.API->>UserManagement.API: RegisterUserCommandHandler<br/>saves User to UserManagementDb
    UserManagement.API->>RabbitMQ: publish UserRegisteredEvent<br/>(exchange: microservices.events,<br/>routing key: user.registered)
    RabbitMQ->>ProductCatalog.API: deliver to queue<br/>product-catalog.user-registered
    ProductCatalog.API->>ProductCatalog.API: UserRegisteredConsumer →<br/>CreateCartCommand → saves<br/>empty Cart to ProductCatalogDb
    ProductCatalog.API-->>RabbitMQ: ack

    Note over Client,ProductCatalog.API: Later, unrelated HTTP calls:
    Client->>ProductCatalog.API: GET /products (public, no token needed)
    Client->>ProductCatalog.API: POST /cart/items<br/>Authorization: Bearer &lt;JWT from login/register&gt;
    ProductCatalog.API->>ProductCatalog.API: validates JWT signature,<br/>userId = token's sub claim
    ProductCatalog.API-->>Client: 401 if no/invalid token;<br/>200 if a Cart row exists for that userId,<br/>404 NOT_FOUND otherwise
```

## Exchange / queue / routing-key convention

- **Exchange**: `microservices.events` — a single durable topic exchange, declared idempotently by both the publisher and the consumer on startup.
- **Routing key convention**: `<domain>.<event-in-past-tense>`. This integration uses `user.registered`.
- **Queue**: `product-catalog.user-registered` — durable, owned and declared by the consumer, bound to the exchange with routing key `user.registered`.
- **Payload**: JSON via `System.Text.Json`. The shared contract, `Shared.Kernel.Events.UserRegisteredEvent`, lives in `Shared.Kernel` so both services compile against the identical shape.

## Why ProductCatalog reacts by creating a cart

`Cart` is keyed by `UserId` with no separate identity concept. Reusing it as an event-driven "known users" replica means ProductCatalog can gate cart access without ever calling UserManagement synchronously:

- `AddCartItemCommandHandler` used to lazily create a cart for *any* `userId`, registered or not.
- Now it 404s (`NotFoundException`) if no `Cart` row exists — and a `Cart` row only exists once `CreateCartCommand` has run for that user, which only happens after `UserRegisteredEvent` was consumed.

**This is now a data-integrity guard layered under real authentication, not a substitute for it.** ProductCatalog validates the same JWT bearer tokens UserManagement issues (shared `Jwt:Key`/`Issuer`/`Audience`, stateless — no network call back to UserManagement), so `userId` on `POST /cart/items` is cryptographically proven, not just a route parameter someone typed in. The cart-existence check's remaining job is narrower than it used to be: it's not standing in for identity verification anymore, it's catching the *eventual-consistency window* between a user registering and `UserRegisteredEvent` actually being consumed — a genuinely authenticated, freshly-registered user can still hit a false 404 if they call the cart endpoint before that event has landed.

`POST /products`, `POST /categories`, and `PUT /inventory/{productId}` also require `.RequireAuthorization()` now, but don't derive anything from the token — any authenticated caller can hit them, since there's no role/admin concept anywhere in this system yet. `GET /products` stays public.

## Known simplifications (this is a teaching PoC, not production-hardened)

- No dead-letter queue or retry policy — `UserRegisteredConsumer` nacks a failed message without requeueing, so a poison message is silently dropped rather than retried or parked for inspection.
- No outbox pattern on the publisher — `RegisterUserCommandHandler` saves the user and publishes the event as two separate steps; a crash between them would save the user without ever publishing the event (and ProductCatalog would then always 404 that user's cart until manually reconciled).
- No schema-versioning strategy for `UserRegisteredEvent` — evolving the contract today means coordinating both services' deploys.
