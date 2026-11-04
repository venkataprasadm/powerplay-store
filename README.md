# IPL Franchise Merchandise Store

Ecommerce store for official IPL franchise merchandise: catalogue and search, cart, checkout, order history, plus a small admin screen.
ASP.NET Core 10 Web API on Azure Cosmos DB, React 18 and TypeScript front end.

Start with [DESIGN.md](DESIGN.md) (architecture decisions, trade-offs, assumptions, limits) and [docs/API.md](docs/API.md) (the contract).
[Requirements traceability](docs/REQUIREMENTS.md) maps every point of the brief to code and tests.
[AI-USAGE.md](AI-USAGE.md) lists where AI tools helped. [web/DECISIONS.md](web/DECISIONS.md) covers front end decisions.

```
src/IplStore.Domain          entities and rules. No dependencies.
  Catalogue/ Carts/ Orders/    Product, Cart, Order and their value types
  Exceptions/                  AppException and one class per business failure
  Common/                      Money and Timestamp rules
src/IplStore.Application     use cases and the storage interfaces.
  Services/                    CatalogService, CartService, CheckoutService, OrderService
  Abstractions/                repository interfaces and query records
  Contracts/                   DTOs returned to the API
  Paging/ Shipping/ Common/    page tokens, shipping policy, options, metrics, retry
src/IplStore.Infrastructure  storage implementations with the same behaviour.
  Cosmos/                      CosmosStore, CosmosCartRepository, CosmosQueries (LINQ), schema, client
  Cosmos/Documents/            storage shapes of products, audit entries, orders and carts
  InMemory/                    the in-memory store used for local runs and fast tests
  Startup/                     creates containers and seeds the catalogue at startup
src/IplStore.Api             HTTP.
  Controllers/                 one controller per resource
  Contracts/Requests|Responses request and response bodies owned by the API
  ErrorHandling/ Middleware/   problem details, correlation ids, access log
  Security/ Hosting/           JWT, rate limits, request timeouts, CORS
tests/IplStore.Tests         xUnit, in folders that mirror the projects: Domain/, Application/, Api/, Storage/, Support/.
web/                         React + Vite + Vitest front end.
scripts/                     Cosmos DB creation script (partition keys, indexing) and index policies.
```

## Run it

Prerequisites: .NET 10 SDK, Node 20+. Docker is optional.

### Option 1: no dependencies (in-memory store)
```bash
dotnet run --project src/IplStore.Api          # http://localhost:5190, seeded catalogue, dev sign-in enabled
```
```bash
cd web && npm install && npm run dev            # http://localhost:5173, proxies /api to the API
```
Use the user switcher in the header (Asha, Ravi, Admin). Data resets when the API restarts.

### Option 2: everything with Cosmos DB, one command
```bash
docker compose up --build                       # web http://localhost:3000, api http://localhost:8080
```
This starts the Cosmos DB Linux emulator, the API (creates database and containers at startup, then seeds) and the web app.
Dev sign-in is enabled in compose for the demo only.

### Option 3: API against a Cosmos DB account or emulator you run yourself
```bash
export Storage__Provider=Cosmos
export Cosmos__ConnectionString='AccountEndpoint=...;AccountKey=...;'   # never commit this; use user-secrets or Key Vault
dotnet run --project src/IplStore.Api
```
Create the database and containers in Azure with `scripts/create-cosmos.sh <resource-group> <account>` or let the API do it (`Cosmos__CreateIfNotExists=true`, the default).
Set `Cosmos__UseGatewayMode=true` and `Cosmos__AllowInsecureEmulatorCertificate=true` for the HTTPS emulator.

## Test it
```bash
dotnet test                                     # 118 backend tests pass, the Cosmos scenarios are skipped without an emulator
cd web && npm test -- --run                     # 53 front end tests
```
Run the storage contract scenarios against a real Cosmos emulator:
```bash
docker run -d --name cosmos -p 8081:8081 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview --protocol http
COSMOS_TEST_CONNECTION='AccountEndpoint=http://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;' \
  dotnet test --filter CosmosStoreContractTests
```
(That key is the published emulator key, not a secret.)

What the backend tests prove:
- **Boundaries:** stock of exactly 1, quantity 0, 1, 10 and 11, price decimals.
- **Concurrency:** 50 shoppers for 5 photos produce exactly 5 orders, Asha against Ravi for the last photo, 10 simultaneous retries of one order, 10 simultaneous copies of one cart request, racing admin price edits.
- **Atomicity:** one failing line deducts nothing and keeps the cart, and a stale product version at commit rolls back every other line.
- **Paging:** keyset paging over 126 products and 25 orders, with inserts during paging.
- **HTTP:** problem details and status codes 400, 401, 403, 404, 409, 412, 428, 429, the v1 contract, correlation ids.

## Configuration

| Setting | Default | Meaning |
|---|---|---|
| `Storage__Provider` | `InMemory` | `Cosmos` or `InMemory` |
| `Cosmos__ConnectionString` | empty | secret, from environment or secret store |
| `Jwt__SigningKey` | none | required (32+ chars) outside Development, from a secret store |
| `Auth__EnableDevTokens` | `false` | `/api/v1/dev/token`. Only appsettings.Development and compose turn it on |
| `RateLimit__Search__PermitLimit` / `WindowSeconds` | 120 / 60 | per user (or IP when anonymous) |
| `RateLimit__Checkout__PermitLimit` / `WindowSeconds` | 10 / 60 | per user |
| `Store__MaxConcurrencyAttempts` | 8 | optimistic retry budget |
| `Seed__Enabled` | `true` | insert the demo catalogue if missing |

Environments override through standard ASP.NET Core configuration: `appsettings.{Environment}.json`, then environment variables (`Section__Key`).

## Endpoints at a glance
`GET /api/v1/products`, `GET /api/v1/products/{id}`, `GET /api/v1/franchises`, `GET|POST|PUT|DELETE /api/v1/cart...`, `PUT /api/v1/orders/{orderId}`, `GET /api/v1/orders`, `PATCH /api/v1/admin/products/{id}` (If-Match), `GET /api/v1/admin/products/{id}/audit`, `/health/live`, `/health/ready`.
Full contract, status codes and examples: [docs/API.md](docs/API.md).
