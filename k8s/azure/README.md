# Running the stack on Azure Kubernetes Service (AKS)

This is the Azure counterpart to `../k8s` (which targets local Docker Desktop Kubernetes only —
see that folder's README). Same five workloads (SQL Server, RabbitMQ, both APIs, the Ocelot
gateway), same `microservices` namespace, same internal service DNS names — the only differences
are: images are pulled from an Azure Container Registry instead of the local Docker cache, and the
JWT signing key / SQL SA password are real generated secrets instead of the placeholder values
`../k8s` ships with for local-only use.

SQL Server and RabbitMQ still run **in-cluster** via the same StatefulSet/Deployment shape as the
local variant — this is a teaching deployment, not a production one, so there's no migration to
Azure SQL Database or Azure Service Bus here. See `../docs/event-driven-architecture.md` and
`../docs/synchronous-communication.md` for how the two APIs talk to each other; nothing about that
changes in AKS.

Full step-by-step Azure Portal walkthrough (resource verification, ACR↔AKS attach, image build/push,
applying these manifests via the Portal's Run Command, and verification) is provided separately as
a deployment guide. This README only documents what's different about the manifests themselves.

## What's different from `../k8s`

- `usermanagement-api/deployment.yaml`, `productcatalog-api/deployment.yaml`,
  `api-gateway/deployment.yaml`: image references point at
  `mspracticalSEA.azurecr.io/<name>:v1` with `imagePullPolicy: IfNotPresent`, instead of
  `<name>:local` / `imagePullPolicy: Never`.
- `usermanagement-api/secret.yaml`, `productcatalog-api/secret.yaml`, `sql-server/secret.yaml`:
  ship with the exact same placeholder values `../k8s` uses (`Jwt__Key:
  "change-this-to-a-secure-secret-key-at-least-32-chars"`, `MSSQL_SA_PASSWORD:
  "YourStrong!Passw0rd"`) — **generate real values before applying these to any cluster.** The
  live `aks-msp-teaching` deployment currently running was applied with real generated values
  (identical `Jwt__Key` across both API secrets, matching SQL password in both connection strings
  and the SQL Server secret) supplied directly via `kubectl apply`/Run command rather than
  committed to git — those real values are **not** reflected in these files.
- Everything else — `namespace.yaml`, both ConfigMaps, all Services, and the RabbitMQ manifests —
  is unchanged from `../k8s`. `RabbitMq__UserName`/`Password` are still `guest`/`guest`: RabbitMQ
  has no external Service exposure (ClusterIP-default, no LoadBalancer), so this is an accepted
  simplification for this teaching scope, not a real credential.
- `sql-server/statefulset.yaml`: memory request/limit lowered from `2Gi`/`4Gi` to `900Mi`/`1536Mi`
  (plus `MSSQL_MEMORY_LIMIT_MB=1200` so SQL Server's own memory manager respects the smaller
  ceiling instead of assuming the host's full RAM), because this cluster's node pool
  (`aks-msp-teaching`, 2× `Standard_A2_v2`, ~2.8Gi allocatable per node) doesn't have 2Gi free per
  node once the cluster's Azure Monitor/Prometheus system pods are accounted for. This is below
  Microsoft's recommended minimum for SQL Server — acceptable for this teaching deployment's light
  data volume, but if this cluster is ever used for anything beyond a demo, add a bigger/second
  node pool (`az aks nodepool add`) and revert to the `../k8s` values instead.
- `sql-server/statefulset.yaml`: added `spec.template.spec.securityContext.fsGroup: 10001`. SQL
  Server 2022's image runs as the non-root `mssql` user (UID 10001) by default; AKS's Azure
  Disk-backed PVCs mount owned by `root` unless a pod `fsGroup` tells the kubelet to chown the
  volume on mount, so without this the container crash-loops on startup with `Access Denied`
  trying to create `/var/opt/mssql/.system`. Docker Desktop's Kubernetes doesn't hit this (its
  bind-mounted volumes behave differently), which is why `../k8s` doesn't need it.

## Note on public IPs

`usermanagement-api`, `productcatalog-api`, and `api-gateway` are all `type: LoadBalancer` (matches
`../k8s`, where Docker Desktop maps these straight to `localhost`). On AKS, **each** provisions its
own public Azure Load Balancer with its own public IP — the two APIs are reachable directly on
their own IPs (ports `8081`/`8082`) in addition to through the gateway (port `8080`). This is
harmless for a teaching deployment but means three public IPs get created, not one; delete the
`microservices` namespace to release all of them (`kubectl delete namespace microservices` via
Run Command).

## Testing the live deployment

Current external IPs for this cluster (`kubectl get svc -n microservices` if these ever change —
Services keep the same IP as long as they aren't deleted/recreated):

| Service | External URL |
|---|---|
| API Gateway | `http://20.205.248.29:8080` |
| UserManagement API (direct) | `http://20.6.122.22:8081` |
| ProductCatalog API (direct) | `http://4.144.199.182:8082` |

Everything below goes through the gateway, same routes as `../k8s/README.md`'s local smoke test —
just against the AKS external IP instead of `localhost:8080`.

**1. Register a user:**
```
curl -i -X POST http://20.205.248.29:8080/auth/register -H "Content-Type: application/json" -d "{\"userName\":\"aksuser\",\"email\":\"aksuser@example.com\",\"password\":\"Secret123!\",\"firstName\":\"AKS\",\"lastName\":\"User\",\"phoneNumber\":\"555-0100\",\"dateOfBirth\":\"1990-01-01\"}"
```
Registering publishes a `UserRegisteredEvent` over RabbitMQ; ProductCatalog's consumer creates an
empty cart for this user shortly after (see `../docs/event-driven-architecture.md`). Use a new
`userName`/`email` if you re-run this — the endpoint 409s on a duplicate.

**2. Log in and capture the token:**
```
curl -i -X POST http://20.205.248.29:8080/auth/login -H "Content-Type: application/json" -d "{\"userName\":\"aksuser\",\"password\":\"Secret123!\"}"
```
Take the `token` field from the response — every request below needs it as
`Authorization: Bearer <token>`.

**3. Create a category** (required before a product can reference it):
```
curl -i -X POST http://20.205.248.29:8080/categories -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"name\":\"Test\",\"parentId\":null}"
```
Take the `id` from the response as `<category-id>` below.

**4. Create a product:**
```
curl -i -X POST http://20.205.248.29:8080/products -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"name\":\"Widget\",\"description\":\"desc\",\"price\":9.99,\"currency\":\"USD\",\"categoryId\":\"<category-id>\",\"imageUrls\":[],\"attributes\":{},\"inventoryCount\":10}"
```
Take the `id` from the response as `<product-id>` below.

**5. Add it to the cart:**
```
curl -i -X POST http://20.205.248.29:8080/cart/items -H "Content-Type: application/json" -H "Authorization: Bearer <token>" -d "{\"productId\":\"<product-id>\",\"quantity\":1}"
```
A populated `owner` field in the response (the registered user's profile) confirms both the
gateway's routing *and* the ProductCatalog → UserManagement synchronous HTTP call
(`../docs/synchronous-communication.md`) are working end-to-end in AKS. A `404` here usually means
the cart wasn't created yet — the `UserRegisteredEvent` from step 1 hasn't been consumed — wait a
few seconds and retry.

**Browsing without auth:** `GET /products` is public —
`curl http://20.205.248.29:8080/products` works with no token.

**Direct-to-service checks:** the same registration/login calls also work against
`http://20.6.122.22:8081` directly (bypassing the gateway); `GET http://4.144.199.182:8082/products`
likewise bypasses the gateway straight to ProductCatalog.

## Rebuilding after a code change

Same caveat as `../k8s`: a `Deployment` doesn't detect that an image with the same tag was
rebuilt. After pushing a new `:v1` image to ACR, force a re-pull with (via Run Command):
```
kubectl rollout restart deployment/usermanagement-api deployment/productcatalog-api deployment/api-gateway -n microservices
```
Since `imagePullPolicy` here is `IfNotPresent` (not `Never`), consider pushing a new tag
(`:v2`, etc.) and updating the Deployment's image field instead — that's the more standard way to
roll out a change and makes rollback trivial.
