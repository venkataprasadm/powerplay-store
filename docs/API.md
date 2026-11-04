# API contract v1 (source of truth for backend and web client)

Base URL `/api/v1` (Vite dev proxy forwards `/api` to http://localhost:5190). JSON, camelCase, enums as strings.
Money is a JSON number with at most 2 decimals, INR, tax inclusive. Timestamps are UTC ISO-8601 strings ending in `Z`.
Auth: `Authorization: Bearer <jwt>`. Products and facets are anonymous. Cart and orders need a user. `/admin/*` needs role `admin`.

## Dev tokens (Development only)
`POST /dev/token` `{ "userId": "asha", "role": "shopper" | "admin" }` -> `{ accessToken, userId, role, expiresAtUtc }`

## Catalogue
`GET /franchises` -> `[{ id: "csk", name: "Chennai Super Kings" }]`
`GET /products?q=&type=&franchise=&pageSize=&pageToken=`
 - `type` one of `Jersey|Cap|Flag|SignedPhotograph|Other` (case-insensitive). `franchise` is a franchise id. `q` is case-insensitive partial name match. Filters combine.
 - `pageSize` default 20, values above 50 are clamped to 50, values below 1 return 400. `pageToken` is opaque.
 - -> `{ items: Product[], nextPageToken: string|null, totalCount: number }`
`GET /products/{id}` -> `Product`, also `ETag` response header.

Product: `{ id, name, type, franchiseId, franchiseName, description, price, stock, inStock, stockStatus, etag }`
 - `stockStatus`: `InStock | LowStock | OutOfStock` (`LowStock` when 1..5). `inStock` is the original v1 field; `stockStatus` was added later as an optional additive field.

Admin (role admin):
`PATCH /admin/products/{id}` headers `If-Match: <etag>` (required, else 428). Body `{ price?: number, stock?: integer, reason: string }` (at least one of price/stock). -> updated `Product` (+ `ETag`). Stale etag -> 412.
`GET /admin/products/{id}/audit?pageSize&pageToken` -> `{ items: [{ id, productId, field: "Price"|"Stock", oldValue, newValue, reason, actor, atUtc }], nextPageToken }` newest first.

## Cart (one cart per user)
All mutations require header `Idempotency-Key: <guid>` (400 if missing or not a GUID). Replaying a key returns the original result and applies nothing twice. Reusing a key for a different change (other product, quantity or operation) returns 409 `idempotency_key_reused` and applies nothing.
`GET /cart` -> `Cart`
`POST /cart/items` `{ productId, quantity }` (adds to an existing line) -> `Cart`
`PUT /cart/items/{productId}` `{ quantity }` -> `Cart`
`DELETE /cart/items/{productId}` -> `Cart`
`POST /cart/accept-changes` -> `Cart` (accepts current prices of all lines)

Cart: `{ lines: CartLine[], itemCount, total, acceptedTotal, requiresAcceptance, canCheckout, shipping, grandTotal }`
CartLine: `{ productId, productName, quantity, acceptedUnitPrice, currentUnitPrice, lineTotal, priceChanged, availableStock, stockStatus, insufficientStock, productType, franchiseId }`
 - `lineTotal` = quantity x currentUnitPrice. `total` is the item total at current prices. `acceptedTotal` is what the user last saw.
 - Added later (additive): `shipping` (flat fee below the free threshold, else 0) and `grandTotal` = `total` + `shipping`, which is what checkout charges. Line `productType` and `franchiseId` (null if the product no longer exists).
 - `requiresAcceptance` true when any line has `priceChanged`. `canCheckout` = lines exist, no priceChanged, no insufficientStock.

## Orders
`PUT /orders/{orderId}` (orderId is a client generated GUID) body `{ expectedTotal: number }` where `expectedTotal` is the cart's `grandTotal` (items plus shipping) -> `201 Order` the first time, `200 Order` with header `Idempotent-Replayed: true` on retry.
`GET /orders?from=&to=&pageSize&pageToken` (from inclusive, to exclusive, UTC instants) newest first -> `{ items: Order[], nextPageToken }`
`GET /orders/{orderId}` -> `Order`
Order: `{ id, userId, status: "Confirmed", createdUtc, lines: [{ productId, productName, quantity, unitPrice, lineTotal }], subtotal, shipping, total }`

## Errors (application/problem+json)
`{ type, title, status, detail, instance, traceId, correlationId, code, errors?, failedLines? }`
 - 400 `validation_failed` with `errors: { field: [messages] }`; also `idempotency_key_required`, `quantity_limit`
 - 401/403 auth; 404 `not_found`
 - 409 `out_of_stock` with `failedLines: [{ productId, productName, requested, available }]` (checkout, cart keeps its content), `insufficient_stock` (cart add/update), `cart_changed` (prices differ from accepted or from expectedTotal, with `newTotal`), `cart_empty`, `order_id_in_use`, `idempotency_key_reused`, `concurrency_conflict`
 - 412 `precondition_failed` (stale If-Match), 428 `precondition_required`, 429 `rate_limited` with `Retry-After` seconds, 503 `dependency_unavailable`
Every response carries `X-Correlation-Id` (client may send one).
