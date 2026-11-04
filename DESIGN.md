# Design

This document records my design decisions. Each one lists the context, the choice, what I rejected, and what it costs.
Numbers marked *estimate* were reasoned, not measured. Anything not verified is said so in [Verification status](#verification-status).

## 0. One page summary

| Concern | Decision |
|---|---|
| Layers | Domain, Application, Infrastructure, Api. Dependencies point inward. Storage behind small interfaces. |
| Data | Cosmos DB. Container `store` (products, audit, orders) partitioned by `/tenantId`. Container `carts` partitioned by `/userId`. |
| Atomic checkout | One Cosmos transactional batch: every stock deduction (ETag guarded), audit rows, and the order. Works because they share a partition. |
| Never oversell | Three layers: domain `Deduct` guard, ETag compare and swap on every product write, all or nothing batch. |
| Idempotency | Cart: `Idempotency-Key` header recorded in the cart document. Checkout: `PUT /orders/{clientOrderId}`, the id is the document id. |
| Pagination | Keyset tokens on immutable sort keys. Never offsets. Page size capped at 50. |
| Consistency | Session. Correctness never depends on it because writes are ETag guarded. |
| Errors | RFC 7807 problem details with a stable `code`. 400, 401, 403, 404, 409, 412, 428, 429, 503. |
| Security | JWT bearer, role policy for admin, secrets only from environment or a secret store, nothing sensitive logged. |
| Multi-tenancy (design only) | `tenantId` is already the partition key and on every document. It becomes the franchise id. |

## 1. ADR-1: Architecture and patterns

**Context.** The brief wants clear layers, dependency injection, testability, and a defence of patterns used and avoided.

**Decision.**
- `IplStore.Domain` holds entities and rules as immutable records: `Product`, `Cart`, `Order`, `AuditEntry`, plus `Money` and `Timestamp`. A state change returns a new instance, so a stale copy cannot be mutated by accident. No framework dependencies.
- `IplStore.Application` holds use cases as plain service classes: `CatalogService`, `CartService`, `CheckoutService`, `OrderService`. It also holds the storage interfaces, the paging token codec, the optimistic retry helper and the metrics.
- `IplStore.Infrastructure` has two implementations of those interfaces: Cosmos DB and in-memory.
- `IplStore.Api` is a thin edge: controllers only translate HTTP to a service call, and one exception handler turns exceptions into problem details.
- Business failures are exceptions derived from `AppException` (validation, not found, conflict, precondition). The API maps each to one status code in one place.
- `TimeProvider` is injected everywhere time matters, so tests control the clock.

**Patterns used, and why.**
- *Repository, shaped by access pattern.* `ICheckoutStore.CommitAsync(order, changes)` is deliberately not `Save(entity)`. The atomic unit is the business operation, and that is what Cosmos can do in one batch.
- *Cosmos DB SDK, with typed LINQ for reads.* Writes use the SDK directly: point reads, ETag guarded replaces and the checkout transactional batch. Read queries are LINQ over the document classes (`CosmosQueries`), so property names are checked by the compiler and match what the serializer writes. `CosmosQueryTests` pin the SQL the SDK generates (stored names, the composite index order, escaped literals) without needing a database.
- *Service layer (use case classes).* Four use cases do not need more structure. Each is easy to trace from controller to storage.
- *Options pattern and lazy configuration.* Keys are read from the final configuration at first use, so environment variables and test overrides behave the same.
- *Test double with a contract.* `InMemoryStore` reproduces ETag compare and swap, atomic batches, duplicate detection and keyset order. The same scenarios (`StoreScenarios`, run by `InMemoryStoreContractTests` and `CosmosStoreContractTests`) run against it and against real Cosmos DB, so the fast tests are not testing a fantasy.

**Patterns avoided, and why.**
- *MediatR and CQRS.* Indirection with no payoff at four use cases. A reader would have to hunt for handlers.
- *Generic repository and Unit of Work.* They hide the one thing that matters here: the atomic boundary is a single partition, not an arbitrary set of entities.
- *Saga, outbox, event sourcing.* Not needed because checkout is atomic within one partition. They add failure modes without solving a problem this system has.
- *Domain events and a rich aggregate graph.* Same reason. Small immutable records are enough.
- *AutoMapper.* DTOs are three lines each and are checked by the compiler.
- *EF Core Cosmos provider.* The brief allows it, but checkout depends on one transactional batch with an ETag condition on every product. In EF Core 9 and 10 the provider's `SaveChanges` writes documents one by one; transactional batches arrive only in EF Core 11, still in preview. Its continuation token paging (`ToPageAsync`) is also marked experimental. EF's main benefits (migrations, relationships, joins) do not apply to a document store.

**Consequences.** Adding a second storage engine means one new implementation plus passing the same contract tests. The cost is a little ceremony: interfaces and DTO records that a smaller app could skip.

## 2. ADR-2: Data model, partition key, consistency, cost

**Containers**

| Container | Partition key | Documents | Notes |
|---|---|---|---|
| `store` | `/tenantId` (value `ipl` today) | `product`, `audit`, `order` (distinguished by `type`) | composite index (`createdUtc`, `id`). `description`, `lines`, `_etag` excluded from the index. |
| `carts` | `/userId` | one cart per user, `id` = user id | index off (point reads only), TTL 30 idle days, holds recent idempotency keys |

**Why `/tenantId` for `store`.** Checkout must change several products, write audit rows and create the order atomically. Cosmos only gives transactions inside one logical partition (transactional batch). Putting these in one partition makes the whole checkout one all or nothing call, with no saga, no compensation and no partial state. Every catalogue and order query also stays in one partition, so nothing fans out across partitions.

**Alternatives rejected.**
- `/productId` or `/franchiseId` for products. Atomic multi-product checkout becomes impossible, so I would need reservations plus compensating writes and a recovery job. Hot items such as the signed photo would also concentrate load on one partition anyway.
- `/userId` for everything. Per-user data is perfect (carts use it), but the stock deduction must be shared across users, so it has to live in one place.
- Orders in their own container partitioned by `/userId`. Better scale for history, but checkout stops being atomic across containers. This is the scale-out path below.

**What this costs.**
- A single logical partition has a 20 GB storage limit and the physical partition throughput ceiling (10,000 RU/s per physical partition). All traffic for the tenant hits it. For one store at this scale that is acceptable. A flash sale on one product contends on one document regardless of key.
- Order documents grow without bound. At about 1 KB each, 20 GB is roughly 20 million orders *(estimate)*.
- **Path at 10x:** keep stock, audit and an order marker in the `store` partition (still atomic) and project orders into a `orders` container partitioned by `/userId` through the change feed. History reads then scale by user. Old orders can move to cold storage with TTL. With multi-tenancy the partition becomes per franchise, which spreads load on its own.

**Query patterns and RU awareness** *(estimates, not measurements)*
- Product by id, order by id, cart by user: point reads, about 1 RU per KB. These are the hot paths.
- Product list and search: single partition query ordered by the composite index. `CONTAINS(name, text, true)` (case-insensitive) is a scan within the partition, fine for a catalogue of thousands, the first thing to replace (Azure AI Search or a normalized search field) at much larger size.
- Product count runs only on the first page. Later pages skip it because the count does not change the user's answer.
- Cart lines resolve with one `id IN (...)` query, not one read per line.
- Order history: single partition, filter by `userId` and time range, newest first through the same composite index in reverse.
- Writes: index exclusions on `description`, `lines` and `_etag` cut write RU. Carts have no index at all. Batch cost is the sum of its operations.
- Throughput: manual 400 RU/s per container in dev. In production use autoscale and alert on 429 rate. The SDK retries 429s (up to 5 attempts, 10 s) before the API returns 503 with `Retry-After`.
- Cross partition queries: none on any user path. Reports across tenants would be cross partition, so they belong on the change feed, not live queries.

**Pagination.** Tokens are keyset cursors (`createdUtc`, `id`), base64 encoded. Sort keys are immutable, so an item can never move between pages. Name is not the sort key for exactly this reason: a rename would shift items. Offsets are never used, so deep pages cost the same as the first. Page size is clamped to 50. A token carries no authority: order queries always filter by the signed-in user, and a tampered token only changes the position.

**Timestamps.** Stored as fixed width UTC strings (`2026-01-01T10:00:00.0000000Z`). Fixed width makes text order equal time order, which keeps `ORDER BY` and range filters correct. The API returns UTC with a `Z`. The UI shows IST (`Asia/Kolkata`) by default, and the date filter converts IST day boundaries to UTC instants.

**Money.** `decimal` in C#, JSON number with at most two decimals, validated at the boundary. INR, tax inclusive. The UI never adds amounts itself, it shows server totals.

**Consistency level.** Session (the account default).
- Why: cheap reads, read your writes inside a client session, no cross region cost.
- Why it is enough: every read-modify-write is guarded by an ETag. A stale read can only cause a 412 or a retry, never a wrong write. This matters because multiple API instances do not share a session token, so one instance may read slightly behind another.
- Why not Strong: higher latency, higher RU for reads, restricted with multi region writes, and it buys nothing the ETags do not already guarantee.
- Why not Eventual: a shopper could add an item and not see it.

**Cosmos client settings.** Singleton client, `RequestTimeout` 10 s, SDK 429 retry enabled, System.Text.Json serializer, gateway mode only for the emulator. The SDK still loads Newtonsoft.Json internally at runtime, so the project references it explicitly. (Bypassing the SDK's build check hid this once: the API failed to start while the tests, which carry Newtonsoft transitively, passed. It was found by running the real API against the emulator.)

**Container creation.** `CosmosSchema.EnsureAsync` (runs at startup, retries while the emulator warms up) and `scripts/create-cosmos.sh` (Azure CLI, for least privilege deployments). Both define the same partition keys and indexes.

## 3. ADR-3: Concurrency and idempotency

**Requirement.** Stock must never go below zero under any load. Retries must never duplicate cart changes or orders. A partial failure must leave consistent state.

**Stock safety, in three layers.**
1. `Product.Deduct` refuses to go below zero.
2. Every product write is a compare and swap on the document ETag (`IfMatchEtag`). If anyone changed the product after we read it, the write fails with 412.
3. Checkout is one transactional batch: all `ReplaceItem` calls with ETags, all audit `CreateItem` calls, and the order `CreateItem`. If any operation fails the whole batch rolls back.

**Checkout algorithm** (`CheckoutService`)
1. Validate `expectedTotal` has at most two decimals.
2. If the order id already exists, return it (replay). If it belongs to another user, 409 `order_id_in_use`.
3. Load the cart. Empty means 409 `cart_empty`, unless this order already exists (a retry that arrives just after success).
4. Retry loop, up to 8 attempts, jittered exponential backoff:
   - Read all products fresh.
   - **Stock first.** Any line missing or short makes the whole order fail with 409 `out_of_stock`, listing every failing line. Nothing is written and the cart is untouched.
   - **Price consent.** If any line's accepted price differs from the current price, or the computed total differs from `expectedTotal`, 409 `cart_changed` with the new total.
   - Build order lines from the fresh name and price. Build stock changes from the versions just read.
   - Commit the batch. On 412 the loop re-reads and re-decides. On a duplicate order id (409 from the batch) it returns the existing order.
5. Clear the cart (best effort, see failure modes), return 201.

Why re-read in the retry: a lost race means someone else committed. The next round sees current stock, so the loser gets either success or a correct `out_of_stock`, never a wrong deduction. After a lost round we also check whether the winner was a copy of this very order, so a concurrent retry gets the order back instead of a false out of stock.

**Known limit: a hot SKU.** With optimistic concurrency, 20 shoppers buying the same plentiful product conflict with each other, and some can exhaust their 8 attempts and get 409 `concurrency_conflict` ("please retry") even though stock exists. Correctness holds (a test proves stock and orders always reconcile) but throughput on one hot document is limited by the conflict rate. **Alternative for a flash sale:** a conditional batch patch (`Increment -qty` with filter predicate `stock >= qty`) removes retries entirely, but then the audit row cannot record exact old and new values. I chose ETags because the brief requires optimistic concurrency and an exact audit trail.

**Idempotency.**
- *Cart changes.* Every mutation needs `Idempotency-Key` (GUID, 400 otherwise). The cart document stores its last 20 keys with the resulting lines. A repeated key returns the recorded result and writes nothing. Two simultaneous copies race on the cart ETag. The loser retries, finds the key recorded, and returns the winner's result. Replays return the original lines, with price flags recomputed live. Limit: a key older than the last 20 requests would be applied again. A shopper cannot plausibly replay a request that old.
- *Checkout.* `PUT /orders/{orderId}` with a client GUID. The order id is the document id, so Cosmos itself rejects a second create. First call returns 201. A replay returns 200 with `Idempotent-Replayed: true`. Using PUT makes the retry semantics obvious to any HTTP client or proxy.
- *Admin updates.* `If-Match` carries the ETag the admin saw. Missing gives 428. Stale gives 412 and nothing is applied, never a silent merge.

**Failure modes**

| Failure | What happens |
|---|---|
| Two checkouts for the last unit | One wins. The other re-reads and gets 409 `out_of_stock` with the failing line. Cart kept. |
| Response lost after commit, client retries | Same order id returns the same order (200, replayed). Stock unchanged. |
| Batch fails halfway | Impossible by design: it is atomic. Tests prove a stale version on one line rolls back every other line. |
| Crash between commit and cart clear | Order exists, cart still has items. A retry with the same order id returns the order and the cart is cleared. A new order id would buy again. Mitigation: the UI keeps the order id until success. Not fixed server side (see limitations). |
| Price changes while the user is on the checkout page | 409 `cart_changed` with the new total. Nothing deducted. The user accepts and retries. |
| Cosmos 429, timeout, 503 | SDK retries 429 first. Then the API returns 503 with `Retry-After`. Writes are idempotent so the client can retry safely. |
| Two admins edit the same product | One gets 412 and must reload. |
| Product deleted while in a cart | Line shows unavailable and checkout fails with `out_of_stock`. |
| Duplicate client order id from another user | 409 `order_id_in_use`. The other user's order is never revealed. |

## 4. ADR-4: Evolution to per franchise stores (design only, not built)

**Goal.** Each franchise runs its own store: its own catalogue, stock and sales reports, with data isolated from the others.

**Data model and partition key.** `tenantId` is already on every `store` document and is already the partition key, supplied from one place (`CosmosOptions.TenantId`). The change is that the value becomes the franchise id (`csk`, `mi`) and comes from a request scoped `ITenantContext`, not configuration. Isolation then becomes structural: every query sets a partition key, so a query physically cannot read another franchise. Large franchises each get their own logical partition and spread across physical partitions. Cross tenant reporting does not use live queries, it goes through the change feed into an analytics store.

**Carts.** A shopper's cart becomes per store: document id `{userId}:{tenantId}` in the same `/userId` partition. An order then belongs to one franchise, so checkout stays a single partition batch. Cross franchise baskets need a different design (one payment, several fulfilments) and are out of scope.

**API.** New routes `/api/v2/franchises/{franchiseId}/products ...` with the same shapes. The tenant comes from the route and is checked against the token. v1 stays alive during migration as a facade: product ids are unique, so it can resolve the tenant from the product. Existing v1 clients keep working and are given a sunset date.

**Authorization.** Tokens carry a `tenant` claim (the franchises a user may administer) and roles `shopper`, `franchise-admin`, `platform-admin`. A policy `TenantAccess` compares the route tenant to the claim. Repositories take the tenant from `ITenantContext`, never from request bodies. Defence in depth: every document read verifies `doc.tenantId`, and tests assert that a franchise admin of `csk` gets 404 for an `mi` product.

**Migration of existing data.** Existing documents all have `tenantId = ipl`. A partition key value cannot be edited in place, so each document is rewritten.
1. Add `ITenantContext` and thread it through the repositories. Behaviour is unchanged (single tenant).
2. Run a migration job over `store`: read each product, audit and order, derive the franchise (product `franchiseId`; order lines are split per franchise), write with the new `tenantId`. Idempotent (create if absent), resumable, batched, throttled to a fraction of provisioned RU.
3. Keep writing to both during the cutover, verify counts and a checksum of stock per product, then switch reads and delete the old `ipl` documents.
4. Carts migrate lazily: on first read of an old cart, split lines by franchise.
5. Historic orders that span franchises are split into per franchise orders for reporting. The original stays available to the shopper in history.

**What I would do first.** Step 1, plus a test suite that fails if any repository query lacks a partition key or ignores the tenant. That makes isolation a property of the code base before a single franchise is onboarded.

**Risks.** Noisy neighbours: use autoscale per container and a per tenant rate limit, and give a very large franchise a dedicated container. Migration of live stock: freeze admin edits for the cutover window or dual write.

## 5. Security
- **Authentication.** JWT bearer. Validated: issuer, audience, signature (HS256 only), lifetime (30 s skew). In production tokens come from an identity provider (Entra ID or B2C) and the API validates them, ideally with asymmetric keys and OIDC discovery. `POST /dev/token` exists only so the demo runs without a provider, is a 404 unless `Auth:EnableDevTokens` is true, and only Development settings and the compose demo enable it.
- **Authorization.** Anonymous: catalogue and franchises. Signed in user: own cart and orders. Role `admin`: product updates and the audit trail. The user id comes only from the validated token. Another user's order returns 404, not 403, so ids cannot be probed.
- **Secrets.** Nothing secret is in source. `Jwt:SigningKey` and `Cosmos:ConnectionString` come from environment variables or a secret store (user secrets locally, Key Vault in Azure with managed identity). The API refuses to start outside Development without a 32 character key. Better still in production: Cosmos with Entra ID RBAC and no key at all.
- **Input validation.** Every input is validated. Model binding catches malformed JSON, missing fields and types. The domain validates ranges, decimal places and enum values. Cosmos queries are built with typed LINQ, never by string concatenation: the SDK writes each value as an escaped JSON literal, and tests prove search text cannot break out of it. Ids are pattern checked. Search text is length limited. Correlation ids are accepted only if they match a safe pattern, which prevents log forging.
- **Logging.** Structured JSON. Only the path is logged, never query strings or bodies. No tokens, no payloads. User ids appear in business log lines, which is pseudonymous. 500 responses never include exception text.
- **HTTP hygiene.** `nosniff`, `Cache-Control: no-store` on authenticated requests, no CORS needed because the front end is same origin through the proxy.
- **Rate limiting.** Search and checkout, per user (per IP when anonymous). Excess returns 429 with `Retry-After`. Limit: the built in limiter is per API instance. Behind several instances use a gateway (API Management or Front Door) or a distributed limiter. Fixed windows allow a burst at a window edge, which is acceptable here.
- **Not done:** HTTPS is expected to terminate at the ingress, no refresh tokens, no account lockout (identity provider's job), page tokens are not signed.

## 6. Reliability
- Timeouts: Cosmos client 10 s, per request timeout 30 s (checkout 20 s, status 504).
- Retries: SDK handles 429 with server supplied waits. Application level retry is only for ETag conflicts, with jittered exponential backoff, capped attempts, and cancellation support.
- Idempotent writes everywhere a client could retry (section 3).
- Graceful degradation: Cosmos 429, timeout or unavailability becomes 503 `dependency_unavailable` with `Retry-After`, not a 500. A failure to clear the cart after a committed order is logged and does not fail the request.
- Health: `/health/live` (process) and `/health/ready` (checks Cosmos with a 3 s timeout, no details exposed).
- Startup: the bootstrapper retries creating containers and seeding for about a minute so the API tolerates a slow emulator.

## 7. Observability
- Structured JSON logs with `CorrelationId` in scope on every line. The id is accepted from `X-Correlation-Id`, generated if absent, echoed in the response and in every problem detail (`correlationId`, `traceId`). The web client sends one per request and shows it as "Reference" in error banners.
- One access log line per request: method, path, status, duration.
- Metrics hooks: .NET `Meter` named `IplStore` with counters `ipl_orders_placed`, `ipl_checkout_rejected`, `ipl_concurrency_conflicts`, `ipl_idempotent_replays`. Not exported yet: attach OpenTelemetry (`AddMeter("IplStore")`) and an exporter. The conflict counter is the early warning for a hot SKU.
- Tracing: ASP.NET Core and the Cosmos SDK emit `Activity` spans. The correlation id is tagged on the current activity so logs and traces join up.

## 8. Assumptions
- Prices are INR, tax inclusive, at most two decimals, between 0.01 and 10,000,000.
- **Shipping is a flat fee below a threshold.** `ThresholdShippingPolicy` reads `Shipping:FlatFee` (default ₹99) and `Shipping:FreeFrom` (default ₹999): orders at or above the threshold ship free, so every total in the brief's walkthrough is unchanged. The cart and checkout call the same policy on the same input (item total at current prices), and the cart returns `shipping` and `grandTotal`, so the total the shopper sees is exactly the total checkout charges. Setting `FlatFee` to 0 turns shipping off.
- One cart per user. The cart does not reserve stock. Stock is checked when adding and again at checkout. Reserving would need expiry and a release job.
- A cart holds at most 20 different products (keeps the batch well under Cosmos' 100 operation limit).
- Adding a product already in the cart increases that line (still capped at 10).
- The cart keeps the price the shopper first saw. If it changes, the cart flags it, shows the new total, and checkout stays blocked until the shopper accepts.
- The client must send `expectedTotal` at checkout, so a shopper can only pay the total they saw.
- Franchise filter uses the franchise id (`csk`). The UI gets names from `/franchises`.
- Product ordering is catalogue order (creation time). The brief's example lists MI Jersey and CSK Cap first, which is exactly insertion order.
- `pageSize` above 50 is clamped. Below 1 is a 400.
- Order history `from` is inclusive and `to` exclusive, both UTC instants. The UI sends IST day boundaries.
- Seed data: the four walkthrough products with the stock from the brief, plus two more for paging.
- Orders are always `Confirmed`. There is no payment, cancellation or return flow.

## 9. Beyond the minimum (what I added and why)
- **Price consent.** Accept new total endpoint, `expectedTotal` at checkout, and 409 `cart_changed`, so nobody is charged more than they saw.
- **Order id ownership.** A second user cannot claim or read an order id. Prevents id probing and cross user replay.
- **Replay edge cases.** A retry that arrives after the cart was cleared, and a concurrent retry of the same order, both return the original order instead of an error.
- **Admin controls.** A mandatory reason, 428 when `If-Match` is missing, an audit endpoint that pages, and an admin screen that shows the 412 conflict path.
- **Stock status.** `InStock`, `LowStock`, `OutOfStock`, which is also the backwards compatible v1 change (section 10).
- **Cart limits.** Maximum distinct products, bounded idempotency history, TTL on idle carts.
- **Idempotency key reuse is detected.** Each recorded cart request id also stores a fingerprint of what it asked for (`add:csk-cap:3`). The same id sent with a different change returns 409 `idempotency_key_reused` instead of silently replaying an unrelated result. This catches client bugs that would otherwise lose a shopper's change. Entries written before the fingerprint existed have none and replay as before.
- **Shipping in the cart.** Flat fee under a threshold, shown in the cart and checkout before the shopper commits (section 8).
- **Add to cart feedback.** The header cart badge bumps with a floating +N, a confirmation card offers View cart / Checkout, and each product shows how many are already in the cart. Details and the accessibility reasoning are in `web/DECISIONS.md`.
- **Operational.** Correlation ids with log forging protection, health checks, metrics, startup retry, `no-store` on private data.
- **Storage contract tests** that run the same scenarios on the in-memory double and on Cosmos.
- **Front end:** keyed retry behaviour, persistent order id across reloads, field level server errors, focus management, axe tests (see `web/DECISIONS.md`).

## 10. API versioning and the backwards compatible change
Routes are versioned in the path (`/api/v1`), explicitly, with no extra package, so the contract is greppable.
- **The change:** `stockStatus` (`InStock | LowStock | OutOfStock`) was added to the product response. The original `inStock` boolean stays, with the same meaning.
- **Later additive changes, same rules:** the cart gained `shipping` and `grandTotal`, and cart lines gained `productType` and `franchiseId`. `total` keeps its v1 meaning (item total). A v1 client that still sends `total` as `expectedTotal` is protected by the existing price consent rule: when shipping applies it gets 409 `cart_changed` with `newTotal`, and nothing is charged.
- **How existing clients are protected:** the change is additive and optional. Nothing is removed, renamed or retyped. Clients are expected to ignore unknown fields (tolerant reader). A contract test (`Product_list_honours_the_v1_contract_including_the_additive_field`) pins the original fields and their JSON types, so a future edit that breaks a v1 client fails CI. The front end treats `stockStatus` as optional and falls back to `inStock`. A breaking change would go to `/api/v2` and run alongside v1 until clients move.

## 11. Testing
- **Backend (118 passing, 1 skipped theory for Cosmos):** domain boundaries; the SQL generated from the LINQ queries; services against the in-memory store; real concurrency (parallel tasks with forced thread hops); HTTP tests through the real pipeline; storage contract scenarios.
- **Front end (53 passing):** helpers, typed client, double submit and retry key reuse, rejected actions, conflict and stale states, add to cart feedback, shipping in totals, axe checks. Mocks sit at the API client module boundary.
- Concurrency tests were run 40 times in a loop without a failure.
- **Not covered:** browser level end to end tests, load tests, colour contrast (axe cannot compute it in jsdom).

## 12. Known limitations and next steps
1. Hot SKU contention (section 3). Next: conditional patch for flash sale items, or a per product queue.
2. Crash between order commit and cart clear can leave a cart that a new order id could buy again. Next: record the checked out cart version on the order and refuse to reuse it, or clear the cart inside the same atomic unit (put carts in the same partition).
3. Orders share one partition with the catalogue. Next: change feed projection to a per user container (section 2).
4. No real identity provider, no refresh tokens. Next: Entra ID, OIDC code flow with PKCE in the web app (ideally through a backend for frontend).
5. Rate limiting is per instance. Next: gateway level or Redis backed.
6. Name search is a partition scan. Next: a normalized search field, then a search service.
7. Metrics are hooks only. Next: OpenTelemetry exporter, dashboards, alerts on 5xx, conflict rate and 429 rate.
8. No inventory reservation, payments, cancellations or returns. Shipping is one flat fee for all of India (no zones or weights).
9. Idempotency window is the last 20 cart requests.
10. Compose file and CI definition are written but see Verification status for what was actually run.
11. Next for tests: Playwright end to end with axe in a real browser, and a load test of the checkout path.

## Verification status
What I actually ran on 4 November 2026 (macOS, Apple silicon, .NET 10.0.403, Node 24, Docker 29):

| Check | Result |
|---|---|
| `dotnet build` | 0 errors |
| `dotnet test` (no emulator) | 105 passed, 1 skipped (the Cosmos theory) |
| Concurrency, cart and checkout tests repeated 40 times | 0 failures |
| Storage contract scenarios against the real Cosmos DB vNext emulator | 8 of 8 passed |
| API run against the Cosmos emulator, brief's walkthrough driven with curl | matched the brief: order 10,494.00, stock MI 48, CSK 97, KKR 0, replay returns the same order, loser keeps cart, history unchanged by a price change, audit trail written. The race was run both ways (either shopper can win). |
| `docker compose up --build` | all three services started, API retried bootstrap while the emulator warmed up and then seeded, web proxied to the API through the emulator |
| Front end `tsc`, `vitest`, `vite build` | clean, 45 tests passed |
| UI in a browser against the live API | product list rendered, signed in as Asha, double clicking Add to cart produced one item |

Update on 5 November 2026 (shipping, idempotency key reuse, cart line fields, front end feedback): `dotnet test` 112 passed and 1 skipped; front end `tsc`, `vitest` (53 passed) and `vite build` clean; in a browser against the in-memory API, adding to cart showed the +1 bump and the confirmation card, a ₹499 cap showed ₹99 shipping and the order was placed for ₹598.00, and page size 2 showed MI Jersey and CSK Cap with a next page. The new `fingerprint` field is an optional property on the cart document.

Re-check on 5 November 2026 from a fresh clone of `main`: `dotnet test` 112 passed and 1 skipped; front end `vitest` 53 passed and `vite build` clean. `docker compose up --build` started all three services, the API retried until the emulator was ready and then seeded, and, driving the API with curl through the web proxy on port 3000, I signed in as Asha, ordered 2 MI Jerseys for ₹3,998.00 and saw stock go from 50 to 48 in Cosmos. I also ran the API outside Docker against only the compose Cosmos container: a ₹499 cap was ordered for ₹598.00 (₹99 shipping).

Read queries moved from SQL strings to SDK LINQ on 5 November 2026: `dotnet test` 118 passed and 1 skipped (6 new SQL shape tests); the 8 storage contract scenarios passed against the Cosmos vNext emulator before and after the change; with the API on the emulator, page size 2 returned MI Jersey and CSK Cap with a next page, type Cap plus franchise csk returned only CSK Cap, `q=JERS` matched both jerseys, and a search text containing quotes returned no results.

Not run: the GitHub Actions workflow (it was written but never executed), the Cosmos script `scripts/create-cosmos.sh` against Azure (the same schema is created by the app code, which was run against the emulator), load tests, browser end to end tests, colour contrast checks.
The emulator used is the Linux vNext preview. Behaviour on the real service may differ in details such as throughput and indexing limits.
