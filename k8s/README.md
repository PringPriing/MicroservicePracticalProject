# Running the stack on Kubernetes (local, Docker Desktop)

Runs both APIs, the Ocelot API Gateway, SQL Server, and RabbitMQ inside Docker Desktop's built-in Kubernetes cluster, so the whole stack is reproducible from nothing with no host dependencies beyond Docker Desktop. See `../docs/event-driven-architecture.md` and `../docs/synchronous-communication.md` for how the two APIs talk to each other; nothing about either changes here except the hostnames (`rabbitmq`, `sql-server`, `usermanagement-api`, `productcatalog-api` instead of `localhost`).

This is additive — plain `dotnet run` against a local SQL Server (see the root `CLAUDE.md`) and `docker-compose.yml` (RabbitMQ only) still work exactly as before.

## Prerequisites

1. Docker Desktop → Settings → **Kubernetes** → check **Enable Kubernetes** → Apply & Restart.
2. Docker Desktop → Settings → **Resources** → at least 4 CPUs / 8GB RAM (SQL Server alone wants ~2GB).
3. Confirm it's up: `kubectl get nodes` should show one `Ready` node.

## 1. Build the images

Run from the repo root (the Dockerfiles need the whole `src/` tree as build context for project references):

```
docker build -t usermanagement-api:local -f src/UserManagement/UserManagement.API/Dockerfile .
docker build -t productcatalog-api:local -f src/ProductCatalog/ProductCatalog.API/Dockerfile .
docker build -t api-gateway:local -f src/ApiGateway/Dockerfile .
```

Docker Desktop's Kubernetes shares the same local image cache as `docker build`, so no registry push is needed — that's also why the Deployments set `imagePullPolicy: Never`.

**If you rebuild an image after changing code, existing pods will keep running the old one.** `imagePullPolicy: Never` plus reusing the same `:local` tag means Kubernetes has no signal that the image content changed — a `Deployment` only picks up a rebuilt image on pod (re)creation, not automatically. After rebuilding, force it with:
```
kubectl rollout restart deployment/usermanagement-api deployment/productcatalog-api deployment/api-gateway -n microservices
```

## 2. Apply the manifests

In dependency order, so the APIs don't spend their first few restarts crash-looping waiting for SQL Server/RabbitMQ:

```
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/sql-server/
kubectl apply -f k8s/rabbitmq/
kubectl wait --for=condition=ready pod -l app=sql-server -n microservices --timeout=120s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n microservices --timeout=60s
kubectl apply -f k8s/usermanagement-api/
kubectl apply -f k8s/productcatalog-api/
kubectl apply -f k8s/api-gateway/
```

## 3. Verify

```
kubectl get pods -n microservices
```

All five should reach `Running`/`1/1`. Both APIs run `Database.Migrate()` on startup (see `RunMigrationsOnStartup` in their ConfigMaps) — check `kubectl logs -n microservices deploy/usermanagement-api` to confirm `UserManagementDb` was created with no errors.

Then hit everything through the **API Gateway** on `localhost:8080` (Docker Desktop maps `LoadBalancer` services straight to `localhost`) — the two services' own ports (`8081`/`8082`) still work directly too, the gateway is additive, not a replacement:

```
curl -i -X POST http://localhost:8080/auth/register -H "Content-Type: application/json" -d "{\"userName\":\"k8suser\",\"email\":\"k8suser@example.com\",\"password\":\"Secret123!\",\"firstName\":\"K8s\",\"lastName\":\"User\",\"phoneNumber\":\"555-0100\",\"dateOfBirth\":\"1990-01-01\"}"

curl -i -X POST http://localhost:8080/auth/login -H "Content-Type: application/json" -d "{\"userName\":\"k8suser\",\"password\":\"Secret123!\"}"
```

Take the `token` from the login response and use it as a Bearer token — create a category and product (both require auth), then add to cart:

```
curl -i -X POST http://localhost:8080/categories -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"name\":\"Test\",\"parentId\":null}"
curl -i -X POST http://localhost:8080/products -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"name\":\"Widget\",\"description\":\"desc\",\"price\":9.99,\"currency\":\"USD\",\"categoryId\":\"<category id from above>\",\"imageUrls\":[],\"attributes\":{},\"inventoryCount\":10}"
curl -i -X POST http://localhost:8080/cart/items -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"productId\":\"<product id from above>\",\"quantity\":1}"
```
The cart response's `owner` field should be populated with the registered user's profile — proof the gateway is routing correctly *and* the ProductCatalog → UserManagement synchronous call (`docs/synchronous-communication.md`) works end-to-end in-cluster. Calling `POST /cart/items` before `UserRegisteredEvent` has been consumed (a fresh registration, cart not created yet) returns `404` — same eventual-consistency behavior as local dev.

RabbitMQ management UI: `kubectl port-forward -n microservices svc/rabbitmq 15672:15672`, then browse `http://localhost:15672` (guest/guest) and check the `product-catalog.user-registered` queue's message stats.

## Teardown

```
kubectl delete namespace microservices
```

Deletes everything in this doc, including the SQL Server PVC's data. Reapplying from step 2 rebuilds the whole stack from nothing.
