# Requirements traceability

Maps each point of the assessment brief to where it is implemented, tested and explained.
Backend tests live in `tests/IplStore.Tests`, front end tests sit beside the code in `web/src`.

## Catalogue
| Requirement | Implementation | Tests / docs |
|---|---|---|
| Product fields, type, franchise, 4+ seed products across 2+ franchises | `Domain/Catalogue/Product.cs`, seed in `Application/Seeding/SeedData.cs` | `CatalogTests` |
| INR, tax inclusive, max 2 decimals | `Domain/Common/Money.cs` | `DomainTests` |
| Stock never below zero | `Product.Deduct`, atomic checkout batch | `CheckoutTests`, `StoreScenarios` |
| Optimistic concurrency on price and stock | ETag and `If-Match`, 412 on stale | `CatalogTests`, `ApiTests` |
| Audit trail of price and stock changes | audit documents written in the same batch | `CatalogTests`, DESIGN.md ADR-3 |
| Shipping | `ThresholdShippingPolicy` (₹99 below ₹999, configurable), shown in cart as `shipping` and `grandTotal` | `CheckoutTests` (threshold, charged total), `CartPage.test.tsx`, DESIGN.md section 8 |

## Search and listing
| Requirement | Implementation | Tests / docs |
|---|---|---|
| Keyset paging, capped page size, stable order | `Application/Paging/PageToken.cs`, `Infrastructure/Cosmos/CosmosQueries.cs`, composite index (createdUtc, id) | `CatalogTests` |
| Name (case-insensitive, partial), type, franchise, combined | `CatalogService`, `CosmosQueries.Products` | `CatalogTests`, `StoreScenarios`, `CosmosQueryTests` |
| Price and stock status shown | `stockStatus` field | `ProductListPage.test.tsx` |

## Cart
| Requirement | Implementation | Tests / docs |
|---|---|---|
| Add, change, remove, quantity 1 to 10, within stock | `Domain/Carts/Cart.cs`, `CartService` | `CartTests`, `DomainTests` |
| Request ID makes repeats idempotent | `Idempotency-Key` header, last 20 keys stored in the cart with a request fingerprint; a key reused for a different change is 409 | `CartTests`, `useIdempotentAction.ts` |
| Price or stock change flagged, new total accepted before checkout | `expectedTotal` consent on checkout | `CartTests`, `CartPage.test.tsx` |

## Checkout and orders
| Requirement | Implementation | Tests / docs |
|---|---|---|
| Client order ID, retry returns same order | `PUT /orders/{id}`, order id is document id | `CheckoutTests`, `StoreScenarios` |
| Never oversell, reject whole order, name failing lines, keep cart | single partition transactional batch | `CheckoutTests` (concurrency, 40 repeated runs) |
| Order snapshot of name and unit price | `Domain/Orders/Order.cs` | `CheckoutTests` |
| History newest first, paged, date range, UTC stored, IST shown | `OrderService`, `web/src/lib` IST formatting | `OrderHistoryTests`, `OrdersPage.test.tsx` |

## API
| Requirement | Implementation | Tests / docs |
|---|---|---|
| Versioned `/api/v1` | controllers route prefix | `docs/API.md` |
| Backwards compatible change | optional `stockStatus` beside original `inStock` | DESIGN.md section 10 |
| Validation and problem details 400, 404, 409, 412, 429 | `Api/ErrorHandling/ApiExceptionHandler.cs`, `Api/Contracts/Requests` | `ApiTests` |
| Rate limit with Retry-After | `Api/Security/RateLimitingExtensions.cs` | `ApiTests` |

## Front end
| Requirement | Implementation | Tests / docs |
|---|---|---|
| List, details, cart, checkout, order history pages | `web/src/pages` | page tests |
| Paging with a chosen page size (walkthrough: page size X shows 2 of N with a next link) | "Per page" control on the product list | `ProductListPage.test.tsx` |
| Visible confirmation when an item is added | header badge with +N, confirmation card, "in cart" count | `AddedToCart.test.tsx` |
| Client generated request and order IDs, double-submit safe | `useIdempotentAction`, `usePersistentOrderId` | `CartPage.test.tsx`, `CheckoutPage.test.tsx` |
| Error, conflict, out-of-stock, price-changed, loading and retry states | pages and `api/errors.ts` | page tests |
| Typed client, service layer, accessibility basics | `web/src/api`, `a11y.test.tsx` | `client.test.ts` |

## Evolution, architecture and non-functional
| Requirement | Where |
|---|---|
| Multi-tenancy design note | DESIGN.md ADR-4 |
| Layers, DI, patterns used and avoided | DESIGN.md ADR-1 |
| Partition key, consistency, RU cost | DESIGN.md ADR-2 |
| Reliability, security, observability | DESIGN.md sections 5 to 7 |
| Containers and scripts | `scripts/create-cosmos.sh`, index policies, `docker-compose.yml`, `.github/workflows/ci.yml` |
| Assumptions, extras, limitations, next steps | DESIGN.md sections 8, 9, 12 |
| AI usage | `AI-USAGE.md` |

## Walkthrough scenario
The brief's walkthrough (totals 3,998.00, 5,495.00, 10,494.00, the concurrent photo race, the retried order, the price rise) was run end to end against the Cosmos emulator. See DESIGN.md, Verification status.
