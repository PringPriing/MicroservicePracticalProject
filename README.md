# Microservice Practical Project

A .NET 10 microservices system: two domain-focused services (user authentication and a product
catalog), an Ocelot API Gateway in front of them, synchronous and asynchronous inter-service
communication, EF Core persistence per service, and a full Kubernetes deployment.

**GitHub repository:** https://github.com/PringPriing/MicroservicePracticalProject

This file walks through the system area by area — code locations, how each piece works, and how
to run/verify it yourself. Deeper technical detail for two topics lives in dedicated docs and is
linked rather than duplicated: [`docs/event-driven-architecture.md`](docs/event-driven-architecture.md)
and [`docs/synchronous-communication.md`](docs/synchronous-communication.md).

## Architecture at a glance

```
                        ┌─────────────────────┐
   Client ────────────▶ │   API Gateway        │  (Ocelot, :5000 local / :8080 k8s)
                        │   src/ApiGateway     │
                        └──────────┬───────────┘
                                   │  routes by path
                     ┌─────────────┴─────────────┐
                     ▼                            ▼
        ┌───────────────────────┐    ┌────────────────────────┐
        │ UserManagement.API    │    │ ProductCatalog.API      │
        │ /auth/*               │◀───│ /products, /categories, │
        │ (JWT issuer, CQRS)    │ HTTP  /cart, /inventory      │
        │                       │ sync  (JWT validator, CQRS)  │
        └──────────┬────────────┘    └────────────┬────────────┘
                   │ EF Core                       │ EF Core
                   ▼                               ▼
           UserManagementDb                 ProductCatalogDb
              (SQL Server)                    (SQL Server)
                   │                               ▲
                   └──────────▶ RabbitMQ ──────────┘
                       UserRegisteredEvent → CreateCartCommand
```

Each service owns its own database (database-per-service) and is only ever reached through its
own HTTP API — see `CLAUDE.md` for the full architectural conventions (CQRS via MediatR, handler
patterns, validation pipeline, etc.) that both services follow.

---

## Environment Setup

**Required tooling:**
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), with **Kubernetes enabled**
  (Settings → Kubernetes → Enable Kubernetes) — the Kubernetes deployment below runs against
  Docker Desktop's built-in local cluster
- `kubectl` (bundled with Docker Desktop once Kubernetes is enabled)
- A reachable SQL Server instance for local (non-container) development — `Trusted_Connection=True`
  against `localhost` by default, or just use the containerized SQL Server from the Kubernetes setup

**Verify your environment:**
```
dotnet --version        # should report a 10.x SDK
docker version
kubectl get nodes       # should show one Ready node once Kubernetes is enabled in Docker Desktop
```

**Clone and restore:**
```
git clone https://github.com/PringPriing/MicroservicePracticalProject.git
cd MicroservicePracticalProject
dotnet build MicroservicePracticalProject.slnx
```
A successful build confirms the whole toolchain (SDK, NuGet restore, all three services + all four
test projects) is working.

---

## Domain Services

Two independent Minimal API services, each following the same layered pattern
(`API` → `Application` → `Domain` / `Infrastructure`; see `CLAUDE.md`'s Architecture section for
the full convention):

### UserManagement (`src/UserManagement/`)
Domain: user management and authentication.

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/auth/register` | POST | – | Create a user account |
| `/auth/login` | POST | – | Authenticate, returns a JWT |
| `/auth/{id}` | GET | ✔ | Look up a user's profile by id |
| `/auth/profile` | PUT | ✔ | Update the caller's profile |
| `/auth/password` | PATCH | ✔ | Change the caller's password |
| `/auth/validate-token` | GET | ✔ | Confirm the caller's token still resolves to a real user |

Code: `src/UserManagement/UserManagement.API/Endpoints/UserEndpoints.cs`.

### ProductCatalog (`src/ProductCatalog/`)
Domain: e-commerce product catalog, cart, and inventory.

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/products` | GET | – | List/browse products (paged, filterable by category) |
| `/products/{id}` | GET | – | Retrieve a single product |
| `/products` | POST | ✔ | Create a product |
| `/categories` | POST | ✔ | Create a category |
| `/cart/items` | POST | ✔ | Add an item to the caller's cart |
| `/inventory/{id}` | PUT | ✔ | Adjust a product's inventory count |

Code: `src/ProductCatalog/ProductCatalog.API/Endpoints/*.cs`.

**Validation and error handling:** every command/query has a co-located `FluentValidation`
validator, run automatically by a shared MediatR pipeline behavior
(`Shared.Kernel/Behaviors/ValidationBehavior.cs`) before the handler ever executes. Domain/not-found/
conflict errors are mapped centrally by each service's exception-handling middleware in `Program.cs`
to a consistent `{ error: { code, message } }` JSON shape (ProductCatalog) or
`{ title, status, ... }` (UserManagement) — see `CLAUDE.md` for the exact exception-type nuances
between the two services.

**Run each service:**
```
dotnet run --project src/UserManagement/UserManagement.API      # http://localhost:5262
dotnet run --project src/ProductCatalog/ProductCatalog.API      # http://localhost:5288
```

---

## Persistence Layer

Both services use EF Core with a dedicated `DbContext` and its own SQL Server database — no
shared database, no cross-service foreign keys.

| | UserManagement | ProductCatalog |
|---|---|---|
| `DbContext` | `UserManagementDbContext` | `ProductCatalogDbContext` |
| Database | `UserManagementDb` | `ProductCatalogDb` |
| Entities | `User`, `RefreshToken`, `UserProfile` | `Product`, `Category`, `Cart`, `CartItem` |
| Config location | `UserManagement.Infrastructure/Persistence/Configurations/` | `ProductCatalog.Infrastructure/Persistence/Configurations/` |
| Migrations | `UserManagement.Infrastructure/Migrations/` | `ProductCatalog.Infrastructure/Migrations/` |

Entities are plain C# with no framework references (`Domain` project) — state changes go through
named methods (`user.ChangePassword(...)`, `cart.AddOrUpdateItem(...)`), never public setters.
Handlers inject the base `DbContext` (never the concrete `<Service>DbContext`) and call
`db.Set<T>()` directly — there is deliberately no `IRepository<T>`/`IUnitOfWork` abstraction, since
`DbContext`/`DbSet<T>` already is that abstraction.

**Apply migrations locally:**
```
dotnet ef database update --project src/UserManagement/UserManagement.Infrastructure --startup-project src/UserManagement/UserManagement.API
dotnet ef database update --project src/ProductCatalog/ProductCatalog.Infrastructure --startup-project src/ProductCatalog/ProductCatalog.API
```
(In Kubernetes, both APIs instead run `Database.Migrate()` automatically on startup — see below.)

**Add a new migration** (e.g. after changing an entity):
```
dotnet ef migrations add <Name> --project src/<Service>/<Service>.Infrastructure --startup-project src/<Service>/<Service>.API
```
See [`.claude/rules/migrations.md`](.claude/rules/migrations.md) for the idempotency/rollback rules
this project follows for migrations.

---

## Kubernetes Deployment

Both services are containerized (see each service's `Dockerfile`) and deployed to Kubernetes.

> **Note on scope:** this deployment targets **Docker Desktop's built-in local Kubernetes
> cluster**, since no cloud Kubernetes credentials were available for this project. The same
> manifests (`Deployment`/`Service`/`ConfigMap`/`Secret`) would apply to a real managed cluster
> (e.g. AKS/EKS/GKE) with only the image registry and `imagePullPolicy` needing to change
> (`imagePullPolicy: Never` only works because Docker Desktop's Kubernetes shares its image cache
> with `docker build`).

**What's deployed** (`k8s/`):
- `sql-server/` — a `StatefulSet` with a persistent volume, so data survives pod restarts
- `rabbitmq/` — the message broker for the event-driven communication described below
- `usermanagement-api/`, `productcatalog-api/` — each with a `Deployment`, `Service`
  (`LoadBalancer`, mapped straight to `localhost` by Docker Desktop), `ConfigMap`, and `Secret`
- `api-gateway/` — the Ocelot gateway's `Deployment` and `Service`

Full step-by-step instructions (build images, apply manifests in dependency order, verify, tear
down) are in **[`k8s/README.md`](k8s/README.md)** rather than duplicated here. Quick summary:
```
docker build -t usermanagement-api:local -f src/UserManagement/UserManagement.API/Dockerfile .
docker build -t productcatalog-api:local -f src/ProductCatalog/ProductCatalog.API/Dockerfile .
docker build -t api-gateway:local -f src/ApiGateway/Dockerfile .

kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/sql-server/
kubectl apply -f k8s/rabbitmq/
kubectl apply -f k8s/usermanagement-api/
kubectl apply -f k8s/productcatalog-api/
kubectl apply -f k8s/api-gateway/

kubectl get pods -n microservices   # all 5 should reach Running/1/1
```

---

## API Gateway

An [Ocelot](https://github.com/ThreeMammals/Ocelot) API Gateway (`src/ApiGateway/`) sits in front
of both services as a third deployable. It's a pure reverse proxy — no gateway-level
re-authentication; the `Authorization` header passes straight through to whichever downstream
service validates it (both already do, identically, per the shared JWT config described below).

**Why `net8.0` when everything else is `net10.0`:** Ocelot's current stable release only supports
`net8.0`/`net9.0` (`net10.0` support exists only in a beta as of this writing). Since the gateway
has no `ProjectReference` to anything else in the solution — it's pure routing configuration, no
shared code — it can safely target an older TFM on its own without affecting the rest of the
solution.

**Routing table** (`src/ApiGateway/ocelot.json` for Kubernetes, `ocelot.Development.json`
overriding the downstream hosts to `localhost` ports for local dev):

| Upstream | Routed to | Methods |
|---|---|---|
| `/auth/{everything}` | UserManagement | GET, POST, PUT, PATCH, DELETE |
| `/products` | ProductCatalog | GET, POST |
| `/products/{everything}` | ProductCatalog | GET |
| `/categories` | ProductCatalog | POST |
| `/cart/{everything}` | ProductCatalog | POST |
| `/inventory/{everything}` | ProductCatalog | PUT |

**Load balancing:** every route sets `LoadBalancerOptions: { Type: RoundRobin }`. In the Kubernetes
deployment, replica-level balancing across pods already happens for free at the Kubernetes Service
layer regardless of Ocelot (the Service is a stable DNS name backed by however many pod replicas
exist) — Ocelot's own round-robin is correctly configured but pointed at that one stable Service
name rather than individual pod IPs, since pod IPs are dynamic and unsuitable for static Ocelot
config. To observe Ocelot's *own* round-robin directly, run a second instance of a service locally
on a different port and list both under one route's `DownstreamHostAndPorts` in a copy of
`ocelot.Development.json`.

**Run and verify locally:**
```
dotnet run --project src/UserManagement/UserManagement.API
dotnet run --project src/ProductCatalog/ProductCatalog.API
dotnet run --project src/ApiGateway                          # http://localhost:5000
```
Then repeat any of the endpoint calls above against `localhost:5000` instead of the services'
own ports (`5262`/`5288`) — identical responses confirm the gateway is routing correctly. In
Kubernetes, the equivalent front door is `localhost:8080` (see `k8s/README.md`), verified live
end-to-end there: registering a user, logging in, and calling `POST /cart/items` through the
gateway returns a `200` with the cart's `owner` field populated — proof the gateway correctly
routes to both services *and* that the synchronous cross-service call below keeps working when
fronted by the gateway.

---

## Synchronous Communication

`POST /cart/items` (ProductCatalog) synchronously calls `GET /auth/{id}` (UserManagement) over
HTTP to enrich the returned cart with the owner's profile (`CartDto.Owner`). Full design rationale,
sequence diagram, and known simplifications are in
**[`docs/synchronous-communication.md`](docs/synchronous-communication.md)**. Summary:
- `IUserManagementClient`/`UserManagementHttpClient` (`ProductCatalog.Infrastructure/Clients/`) —
  a typed `HttpClient` (`Services:UserManagement:BaseUrl` config: `localhost:5262` locally,
  `usermanagement-api:8081` in Kubernetes).
- The caller's own bearer token is forwarded as-is (no separate service identity) — extracted from
  the incoming request in `CartEndpoints.cs` and passed explicitly through
  `AddCartItemCommand.BearerToken` down to the handler and client.
- Failures (404, timeout, unreachable) degrade gracefully — the cart write still succeeds with
  `Owner: null` rather than failing outright, since this call is enrichment, not identity
  verification (identity is already cryptographically proven by the JWT itself).

**Verify:** register + log in on UserManagement, then `POST /cart/items` on ProductCatalog with
that token — the response's `owner` field should be populated. Stop UserManagement and repeat the
call to confirm it still returns `200` with `owner: null`.

---

## Asynchronous Messaging with Event Bus

RabbitMQ-based event-driven communication: after a user registers, UserManagement publishes a
`UserRegisteredEvent`; ProductCatalog consumes it and provisions an empty cart for that user. Full
design rationale, sequence diagram, exchange/queue/routing-key conventions, and known
simplifications (no DLQ/retry, no outbox pattern) are in
**[`docs/event-driven-architecture.md`](docs/event-driven-architecture.md)**. Summary:
- **Publisher:** `RegisterUserCommandHandler` (UserManagement) → `IEventBus`/`RabbitMqEventBus` →
  topic exchange `microservices.events`, routing key `user.registered`.
- **Subscriber:** `UserRegisteredConsumer` (ProductCatalog, a `BackgroundService`) → dispatches
  `CreateCartCommand`, which creates the `Cart` row.
- **Effectiveness validated by:** `AddCartItemCommandHandler` requiring that `Cart` row to already
  exist (404 otherwise) — this is only possible because the event was actually published,
  delivered, and consumed. The RabbitMQ management UI (`localhost:15672`, guest/guest) lets you
  watch the `product-catalog.user-registered` queue's message count go from 1 to 0 as the consumer
  processes it.

**Verify:** register a user, then (after a brief moment for async processing) call
`POST /cart/items` for that same user — a `200` (rather than `404`) confirms the whole event chain
worked.

---

## Documentation

This file, together with `CLAUDE.md` (architecture conventions and code-style rules for anyone —
human or AI — working in this codebase), `k8s/README.md` (deployment runbook), and the two `docs/`
files linked above (event-driven and synchronous communication design docs), make up the complete
documentation set for the project.

---

## Running the tests

```
dotnet test tests/UserManagement/UserManagement.Application.Tests/UserManagement.Application.Tests.csproj
dotnet test tests/UserManagement/UserManagement.API.Tests/UserManagement.API.Tests.csproj
dotnet test tests/ProductCatalog/ProductCatalog.Application.Tests/ProductCatalog.Application.Tests.csproj
dotnet test tests/ProductCatalog/ProductCatalog.API.Tests/ProductCatalog.API.Tests.csproj
```
Handler/query tests use EF Core's InMemory provider; anything touching real relational concurrency
(ProductCatalog's atomic inventory update, its HTTP-level tests) uses a SQLite shared-cache
in-memory database instead — no SQL Server or RabbitMQ instance is required to run the test suite.

## Known gaps and simplifications

This is a teaching/practical project, not a production system. Documented honestly rather than
glossed over:
- **No cloud deployment** — the Kubernetes manifests target Docker Desktop's local cluster, not a
  provisioned cloud cluster (no credentials were available); see the note under Kubernetes
  Deployment above.
- **No DLQ/retry/outbox pattern** for the RabbitMQ integration — see
  `docs/event-driven-architecture.md`'s "Known simplifications" section.
- **No retry/circuit-breaker or caching** for the synchronous HTTP call, and no self-only
  authorization on `GET /auth/{id}` — see `docs/synchronous-communication.md`'s "Known
  simplifications" section.
- **No role/admin concept anywhere** — every authenticated endpoint is callable by any
  authenticated user; there is no ownership or permission model beyond "has a valid JWT."
- **`UserManagement`'s `RefreshToken` and `UserProfile` entities exist but are unused** — login
  only ever issues an access token; no handler reads or writes those tables yet.
