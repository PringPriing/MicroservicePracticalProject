# Synchronous communication: ProductCatalog → UserManagement

UserManagement and ProductCatalog otherwise never talk to each other synchronously — JWT validation is stateless (ProductCatalog checks the signature locally, no call back to UserManagement) and the only other integration is the asynchronous `UserRegisteredEvent` flow (see `docs/event-driven-architecture.md`). This document describes the one synchronous, request/response HTTP integration between them.

## Flow

```mermaid
sequenceDiagram
    participant Client
    participant ProductCatalog.API
    participant UserManagement.API

    Client->>ProductCatalog.API: POST /cart/items<br/>Authorization: Bearer &lt;JWT&gt;
    ProductCatalog.API->>ProductCatalog.API: AddCartItemCommandHandler<br/>validates inventory, updates Cart, saves
    ProductCatalog.API->>UserManagement.API: GET /auth/{userId}<br/>Authorization: Bearer &lt;same JWT, forwarded&gt;
    UserManagement.API->>UserManagement.API: GetUserByIdQueryHandler<br/>reads User from UserManagementDb
    UserManagement.API-->>ProductCatalog.API: 200 UserDto, or 404 if not found
    ProductCatalog.API-->>Client: 200 CartDto with Owner populated<br/>(Owner: null if the call above failed or timed out)
```

## Endpoint contract

- **`GET /auth/{id}`** (UserManagement.API) — `.RequireAuthorization()`, same JWT bearer scheme as every other endpoint. Returns `UserDto { id, userName, firstName, lastName, phoneNumber, dateOfBirth }` (200) or 404 if no such user exists. No self-only ownership check — any authenticated caller can look up any user id, consistent with this repo's existing no-role/no-ownership-check philosophy for other authorized endpoints (`POST /products`, `POST /categories`, `PUT /inventory/{productId}`).
- **Caller**: `IUserManagementClient`/`UserManagementHttpClient` in `ProductCatalog.Infrastructure`, a typed `HttpClient` registered via `AddHttpClient` with a 5-second timeout and a base URL from `Services:UserManagement:BaseUrl` (per-environment: `http://localhost:5262` locally, `http://usermanagement-api:8081` inside the Kubernetes cluster).
- **Auth forwarding**: the caller's own bearer token is forwarded as-is to UserManagement rather than using a separate service identity. `CartEndpoints.cs` extracts the raw `Authorization` header and passes it explicitly through `AddCartItemCommand.BearerToken` down to the handler and client — no `IHttpContextAccessor`, matching how `userId` itself already flows from claims → endpoint → command in this codebase.

## Why synchronous here, and why this doesn't replace the event-driven integration

This call exists purely for **data enrichment** — showing the cart owner's profile alongside their cart — not identity verification. Identity is already cryptographically proven by the JWT itself; ProductCatalog doesn't need to ask UserManagement "is this a real user" over HTTP, so this call would be redundant for that purpose. It's also not a replacement for the async `UserRegisteredEvent` flow: that flow is what makes a `Cart` exist for a user in the first place (a provisioning concern), while this call only decorates an already-successful cart write with extra display data (a read concern). The two integrations serve different purposes and can fail independently without affecting each other.

Because this is a genuine synchronous dependency, `POST /cart/items` now has a live runtime dependency on UserManagement.API being reachable that didn't exist before. To keep that dependency from being a hard one, failures degrade gracefully (see below) rather than failing the cart write outright.

## Known simplifications (this is a teaching PoC, not production-hardened)

- No retry or circuit-breaker policy — just a flat 5-second `HttpClient` timeout. A slow-but-not-quite-timed-out UserManagement still adds real latency to every `POST /cart/items` call.
- On any failure (404, timeout, connection refused), `UserManagementHttpClient.GetUserByIdAsync` returns `null` and the cart write still succeeds with `Owner: null` — there's no distinction surfaced to the caller between "profile not found" and "UserManagement is down."
- No caching — every cart-item add makes a fresh call, even for the same user adding multiple items in quick succession.
- No self-only authorization on `GET /auth/{id}` — any authenticated user's token can look up any other user's profile by id.
- `Services:UserManagement:BaseUrl` is a hardcoded per-environment config value, not service discovery — adding a third consumer of this endpoint means hardcoding the URL again wherever it's called from.
