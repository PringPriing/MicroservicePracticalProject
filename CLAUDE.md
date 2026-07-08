# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build MicroservicePracticalProject.slnx                                   # build everything

dotnet run --project src/UserManagement/UserManagement.API                       # run UserManagement (JWT auth, CQRS, EF Core)
dotnet run --project src/ProductCatalog/ProductCatalog.API                       # run ProductCatalog (products, cart, inventory, CQRS, EF Core)
dotnet run --project src/ApiGateway                                              # run the Ocelot API Gateway (localhost:5000, routes to both services above)

dotnet test tests/UserManagement/UserManagement.Application.Tests/UserManagement.Application.Tests.csproj    # UserManagement handler tests
dotnet test tests/ProductCatalog/ProductCatalog.Application.Tests/ProductCatalog.Application.Tests.csproj    # ProductCatalog handler tests
dotnet test tests/ProductCatalog/ProductCatalog.API.Tests/ProductCatalog.API.Tests.csproj                    # ProductCatalog HTTP/integration tests
dotnet test <above csproj> --filter "FullyQualifiedName~LoginUserCommandHandlerTests"   # run a single test class/method

dotnet ef migrations add <Name> --project src/UserManagement/UserManagement.Infrastructure --startup-project src/UserManagement/UserManagement.API   # add a migration
dotnet ef database update --project src/UserManagement/UserManagement.Infrastructure --startup-project src/UserManagement/UserManagement.API         # apply migrations
# swap UserManagement for ProductCatalog in the two `dotnet ef` commands above to target that service
```

Compiled language — `dotnet build`/`dotnet run` both compile first. No lint script or `.editorconfig` is configured in this repo; follow the Code Style section below by hand. There is no seed script for either service. UserManagement has `POST /auth/register` to create data; ProductCatalog has `POST /categories` and `POST /products` for the same purpose (a category must exist before a product can be created against it).

To run the whole stack (both APIs, SQL Server, RabbitMQ) on a local Kubernetes cluster instead of plain `dotnet run`, see `k8s/README.md` — additive, doesn't replace the commands above.

## Architecture

Request flow: **Minimal API endpoint → MediatR command/query → handler → `DbContext`**

- `src/<Service>/<Service>.API/Endpoints/` — Minimal API endpoint groups (`UserEndpoints.MapUserEndpoints`), registered once from `Program.cs`. HTTP concerns only: bind the request, `mediator.Send(...)`, translate the result to an `IResult`. Apply `.RequireAuthorization()` per route that needs a logged-in user. **Endpoints never touch `DbContext` or business rules directly.**
- `src/<Service>/<Service>.Application/<Feature>/{Commands,Queries}/<Name>/` — one file per feature holding the command/query record, its handler, and its `AbstractValidator`. Commands mutate and return minimal results (an ID, or `IRequest` for void); queries use `AsNoTracking()` and project straight to a DTO, never an entity.
- `src/<Service>/<Service>.Application/Behaviors/ValidationBehavior.cs` — MediatR pipeline behavior that runs every registered `IValidator<TRequest>` before the handler and throws `FluentValidation.ValidationException` on failure. Handlers never validate manually.
- `src/<Service>/<Service>.Domain/Entities/` — plain C# entities (`User`, `RefreshToken`, `UserProfile`), no framework references. State changes go through named methods (`user.ChangePassword(...)`, `user.UpdateProfile(...)`), not public setters.
- `src/<Service>/<Service>.Infrastructure/` — `<Service>DbContext`, `Persistence/Configurations/*Configuration.cs` (`IEntityTypeConfiguration<T>`), EF Core `Migrations/`, and service implementations (`JwtTokenService` for `ITokenService`). **Handlers inject the base `DbContext`, never the concrete `<Service>DbContext`** — `Program.cs` registers the concrete context normally, then forwards it with `AddScoped<DbContext>(sp => sp.GetRequiredService<UserManagementDbContext>())`.
- `src/Shared/Shared.Kernel` — cross-service technical primitives, referenced by both services' `.Application`/`.API` projects: `Exceptions/` (`NotFoundException`, `ConflictException`, `BadRequestException`), `Behaviors/ValidationBehavior.cs` (the same generic MediatR validation behavior, shared), and `ErrorResponse`/`ErrorDetail` (a `{ error: { code, message } }` record pair). UserManagement's `Program.cs`/handlers still use their own older local copies of these (`UserManagement.Application.Behaviors.ValidationBehavior`, `UserManagement.Application.Exceptions.NotFoundException`) predating Shared.Kernel's population — don't assume the two services' exception types are interchangeable; check which namespace a given service actually imports.
- `UserManagement.API`'s auth: `AddAuthentication(JwtBearerDefaults...).AddJwtBearer(...)` validates JWTs issued by `JwtTokenService` (HMAC-SHA256, 1h expiry, `sub`/`unique_name`/`jti` claims); endpoint handlers read the user id via `ClaimsPrincipal.FindFirstValue(JwtRegisteredClaimNames.Sub)`. Passwords are hashed with BCrypt. ProductCatalog validates the same JWTs — it never issues them, only checks their signature against the identical `Jwt:Key`/`Issuer`/`Audience` UserManagement uses (same `AddJwtBearer` wiring, same `MapInboundClaims = false` fix). `POST /cart/items` derives `userId` from the token's `sub` claim (there's no `userId` route param anymore); `POST /products`, `POST /categories`, and `PUT /inventory/{productId}` require `.RequireAuthorization()` too but don't read anything from the token — any authenticated caller can hit them, since there's no role/admin concept anywhere in this system. `GET /products` stays public.
- Asynchronous, event-driven communication between the two services: after `SaveChangesAsync`, `RegisterUserCommandHandler` publishes a `UserRegisteredEvent` (`Shared.Kernel.Events`) via `IEventBus`/`RabbitMqEventBus` (RabbitMQ.Client, topic exchange `microservices.events`, routing key `user.registered`). ProductCatalog's `UserRegisteredConsumer` (a `BackgroundService`) consumes it and dispatches `CreateCartCommand`, which creates an empty `Cart` for that user — this is the reason `AddCartItemCommandHandler` no longer lazily creates a missing cart (see below). See `docs/event-driven-architecture.md` for the full flow, conventions, and known simplifications (no DLQ/retry, no outbox pattern — this is a teaching PoC).
- Synchronous, request/response communication between the two services: `POST /cart/items` (ProductCatalog) calls `GET /auth/{id}` (UserManagement) via `IUserManagementClient`/`UserManagementHttpClient` (a typed `HttpClient`, `Services:UserManagement:BaseUrl` config) to enrich the returned cart with the owner's profile (`CartDto.Owner`), forwarding the caller's bearer token explicitly through `AddCartItemCommand.BearerToken` rather than via `IHttpContextAccessor`. Failures (404, timeout, unreachable) degrade gracefully to `Owner: null` rather than failing the cart write. This is additive to, not a replacement for, the event-driven integration above — see `docs/synchronous-communication.md`.
- `src/ApiGateway` — a third deployable, an Ocelot API Gateway sitting in front of both services (no `ProjectReference` to anything else in the solution, so it deliberately targets `net8.0`/Ocelot 24.1.0 stable rather than the rest of the solution's `net10.0`, since Ocelot's `net10.0` support is still beta). Pure reverse-proxy — no gateway-level auth, `Authorization` headers pass through untouched to whichever downstream service actually validates them. Six routes cover every real endpoint group (`/auth/{everything}` → UserManagement; `/products`, `/products/{everything}`, `/categories`, `/cart/{everything}`, `/inventory/{everything}` → ProductCatalog), each with `LoadBalancerOptions: RoundRobin`. `ocelot.json` (no-env default, used in k8s) points at the k8s Service DNS names; `ocelot.Development.json` overrides to `localhost` ports for local `dotnet run`. Local port `5000`; k8s Service on port `8080`.
- ProductCatalog scope, deliberately: product/category creation, product retrieval/browsing, cart add-item, and admin inventory updates only. No payment, no order fulfillment, no pricing/promotions, no recommendations, and cart POST never decrements inventory (it only validates against the current `InventoryCount`) — inventory is only ever mutated via `PUT /inventory/{productId}`.
- `AddCartItemCommandHandler` requires a `Cart` row to already exist for the given `userId` (404 `NotFoundException` otherwise) — it no longer lazily creates one. A `Cart` only exists once the `UserRegisteredEvent` reaction (`CreateCartCommand`) has run for that user. Now that `POST /cart/items` requires a valid JWT, `userId` itself is cryptographically proven — this check's remaining job is catching the eventual-consistency window between a user registering and `UserRegisteredEvent` actually being consumed, not verifying identity. See `docs/event-driven-architecture.md`.
- `POST /categories` and `POST /products` (`CreateCategoryCommand`/`CreateProductCommand`) follow the same command/handler/validator-in-one-file pattern as `AddCartItemCommand`/`UpdateInventoryCommand` and return a DTO of the created resource (not just an ID) — `CreateProductCommandHandler` requires `CategoryId` to already exist (404 otherwise), and `CreateCategoryCommandHandler` requires `ParentId` to already exist when provided. Unlike the other mutation endpoints, `POST /products` returns `201` via `Results.CreatedAtRoute("GetProductById", ...)` and `POST /categories` returns `201` via `Results.Created(...)`, since these are true resource-creation endpoints.
- ProductCatalog's inventory update (`UpdateInventoryCommandHandler`) mutates `InventoryCount` with a single `db.Set<Product>().Where(...).ExecuteUpdateAsync(...)` call guarded by `InventoryCount + delta >= 0` in the `Where` clause — never load the `Product` entity and call `SaveChangesAsync` for this, since that read-then-write path is exactly the race the atomic `ExecuteUpdateAsync` guard exists to prevent. EF Core's InMemory test provider doesn't support `ExecuteUpdateAsync` or real locking, so anything exercising this path needs a real relational provider — the test suite uses a SQLite shared-cache in-memory database (`Data Source=file:<name>;Mode=Memory;Cache=Shared`) for exactly that reason.
- ProductCatalog's `List Products` query rejects `sort=popularity` with a 400 validation error (`ListProductsQueryValidator`) rather than silently ignoring it or ordering by a fake proxy — there's no sales/popularity signal in the data model, and none is planned until an orders/analytics feature exists.
- Known gaps in UserManagement: `RefreshToken` and `UserProfile` have entities, configurations, and migrations, but no handler creates or reads them yet (login only issues an access token). Treat these as unfinished, not as dead code to remove.

## Code Style

- Target `net10.0`, nullable reference types enabled, in every `.csproj`.
- `record` for every command, query, and DTO; `sealed` on every handler and domain/infrastructure service class.
- No `async void` — always `async Task`. No `var` when the type isn't obvious from the right-hand side.
- Minimal comments — code should read clearly from naming alone.
- Test files mirror the `src/` feature path under `tests/<Service>/<Service>.Application.Tests/...` and are named `<Name>HandlerTests.cs` / `<Name>ValidatorTests` cases inside them. Tests use `Microsoft.EntityFrameworkCore.InMemory` with a fresh `Guid.NewGuid()`-named database per test, `FluentAssertions` for assertions, and fake implementations (e.g. `FakeTokenService : ITokenService`) instead of mocking `DbContext` — except anything touching `ExecuteUpdateAsync`/real concurrency, which needs the SQLite shared-cache pattern described in Architecture above.
- ProductCatalog also has a `<Service>.API.Tests` project (`tests/ProductCatalog/ProductCatalog.API.Tests`) for behavior that only exists at the HTTP layer — status codes and the `{ error: { code, message } }` shape from the exception-handler middleware, malformed-JSON-body handling, query-string parsing. It boots the real host via `WebApplicationFactory<Program>` (see `Program.cs`'s trailing `public partial class Program;`) against the same shared-cache SQLite database. Reach for this pattern only when a behavior can't be proven at the handler level; default to handler-level `Application.Tests` otherwise.

## Workflow

**IMPORTANT:** Default to plan mode for any non-trivial task — present a plan and get explicit approval before writing or editing code. Only skip planning for trivial, narrowly-scoped requests (e.g. fixing a typo, adding one small test) where the change is obvious and low-risk.

## IMPORTANT Rules

**IMPORTANT:** All data access must go through a handler that injects `DbContext` and calls `db.Set<T>()` — never introduce `IRepository<T>` or `IUnitOfWork`; EF Core's `DbContext`/`DbSet<T>` already is that abstraction.

**IMPORTANT:** Inject the base `DbContext` in handler constructors, not the concrete `UserManagementDbContext`/`ProductCatalogDbContext` — this repo's DI wiring forwards the concrete context to the base type in `Program.cs`, and handlers are written against the base type throughout.

**IMPORTANT:** Never edit an EF Core migration file under `Migrations/` once it may have been applied anywhere — add a new migration with `dotnet ef migrations add` instead.

**YOU MUST** keep business logic in `Application` (handlers) or `Domain` (entity methods) — never in the `API` layer, and never call one service's `DbContext` from the other service.

## Environment

- Requires the .NET 10 SDK and a reachable SQL Server instance (`Trusted_Connection=True` against `localhost` by default; `UserManagementDb` and `ProductCatalogDb` respectively).
- No `.env` file / environment-variable convention is used. Configuration (`ConnectionStrings:DefaultConnection`, plus `Jwt:Key`/`Jwt:Issuer`/`Jwt:Audience` for both services now — ProductCatalog validates the tokens UserManagement issues, so the values must be identical) lives in `appsettings.json` per API project — `appsettings.Development.json` currently only overrides logging. The `Jwt:Key` checked into `appsettings.json` is a placeholder dev secret, not something to reuse as-is.
- Tests don't need SQL Server — handler-level tests run against EF Core's InMemory provider, and anything needing real relational behavior (ProductCatalog's inventory concurrency tests, its `API.Tests` project) uses a SQLite shared-cache in-memory database instead.

## Repo Conventions

This directory is not currently a git repository (no `.git`, `.gitignore`, or `CONTRIBUTING.md` present) — there's no commit history to confirm this against yet, but once initialized, commit messages should follow [Conventional Commits v1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>[optional scope][!]: <description>

[optional body]

[optional footer(s)]
```

- `type` is required: `feat` (new functionality) or `fix` (bug fix) at minimum; also use `build`, `chore`, `ci`, `docs`, `style`, `refactor`, `perf`, `test` as fitting (e.g. `refactor` for handler cleanup, `test` for new handler/validator tests, `docs` for CLAUDE.md updates).
- `scope` is optional, in parentheses right after the type, and should name the affected service or feature area — e.g. `feat(usermanagement)`, `fix(auth)`, `chore(migrations)`.
- Breaking changes: append `!` before the colon (`feat(auth)!: ...`) and/or add a `BREAKING CHANGE: <description>` footer.
- Footers go one blank line after the body, one per line, as `Token: value` (hyphenate multi-word tokens, e.g. `Refs: #12`) — `BREAKING CHANGE` is the one token allowed to keep its space.

Branch names: `<type>/<short-description>` (e.g. `feat/product-catalog-crud`, `fix/refresh-token-expiry`).
