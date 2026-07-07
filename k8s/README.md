# Running the stack on Kubernetes (local, Docker Desktop)

Runs both APIs, SQL Server, and RabbitMQ inside Docker Desktop's built-in Kubernetes cluster, so the whole stack is reproducible from nothing with no host dependencies beyond Docker Desktop. See `../docs/event-driven-architecture.md` for how the two APIs talk to each other; nothing about that changes here except the hostnames (`rabbitmq`, `sql-server` instead of `localhost`).

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
```

Docker Desktop's Kubernetes shares the same local image cache as `docker build`, so no registry push is needed — that's also why the Deployments set `imagePullPolicy: Never`.

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
```

## 3. Verify

```
kubectl get pods -n microservices
```

All four should reach `Running`/`1/1`. Both APIs run `Database.Migrate()` on startup (see `RunMigrationsOnStartup` in their ConfigMaps) — check `kubectl logs -n microservices deploy/usermanagement-api` to confirm `UserManagementDb` was created with no errors.

Then hit the APIs directly on `localhost` (Docker Desktop maps `LoadBalancer` services straight to `localhost`):

```
curl -i -X POST http://localhost:8081/auth/register -H "Content-Type: application/json" -d "{\"userName\":\"k8suser\",\"email\":\"k8suser@example.com\",\"password\":\"Secret123!\",\"firstName\":\"K8s\",\"lastName\":\"User\",\"phoneNumber\":\"555-0100\",\"dateOfBirth\":\"1990-01-01\"}"
```

RabbitMQ management UI: `kubectl port-forward -n microservices svc/rabbitmq 15672:15672`, then browse `http://localhost:15672` (guest/guest) and check the `product-catalog.user-registered` queue's message stats.

```
curl -i -X POST "http://localhost:8082/cart/<the userId from register above>/items" -H "Content-Type: application/json" -d "{\"productId\":\"<some product id>\",\"quantity\":1}"
```
should be `200`; the same call with a random unregistered GUID should `404` — same cart-gating behavior as local dev, just running in-cluster.

## Teardown

```
kubectl delete namespace microservices
```

Deletes everything in this doc, including the SQL Server PVC's data. Reapplying from step 2 rebuilds the whole stack from nothing.
