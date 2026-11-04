# Front-end decisions and trade-offs

Stack: Vite, React 18, TypeScript (strict), react-router-dom v6, Vitest, React Testing Library, vitest-axe.
Run: `npm install`, `npm run dev` (port 5173, proxies `/api` to http://localhost:5190), `npm test -- --run`, `npm run build`.

## Structure
- `src/api/` is the only layer that knows URLs, headers and wire formats. `types.ts` holds the typed models, `errors.ts` holds `ApiError` and message mapping, `client.ts` exports a single `api` object. Pages never call `fetch`.
- `src/hooks/` holds behaviour (async loading, idempotent actions, token paging, persistent order id). `src/state/` holds Auth and Cart contexts. `src/pages/` holds routes. `src/lib/` holds pure helpers (money, time, ids).
- Why: tests mock exactly one module boundary (`../api/client`), and a backend contract change touches one folder.

## State management
- React Context for auth and cart only. Everything else is local state plus a small `useAsync` hook.
- Rejected Redux and React Query: the app has about six screens, and the interesting logic (idempotency, retry) is easier to reason about as 30 lines of my own code than as a library configuration. Trade-off: no request cache or dedupe. Revisiting a page re-fetches. That is fine at this size and always fresh.
- The server is the source of truth for the cart. The context only keeps the last server response, so there is no client-side total arithmetic that could disagree with the server.

## Idempotency and id generation
- Ids are generated in the browser with `crypto.randomUUID()` (with a fallback).
- `useIdempotentAction` ties one idempotency key to one user intent, where intent means the call's arguments:
  - A re-entrancy ref ignores a second click while a call is in flight, so a double click sends one request. The button is also disabled while pending, but the ref closes the gap before React re-renders.
  - After a failure the same intent keeps the same key. A timeout may have been applied by the server, and replaying the key returns the original result instead of doing it twice.
  - The key is renewed after success. It is also renewed when the arguments change (for example a different quantity), because that is a different intent and reusing a key would make the server return the wrong stored result.
- Checkout uses `PUT /orders/{orderId}`. The order id is stored per user in `sessionStorage` and only renewed after the order is confirmed. It survives failures and page reloads, so "I refreshed after a timeout and pressed Place order again" hits the same order. Trade-off: sessionStorage is per tab, so two tabs would use two ids. The cart is per user on the server, so the second checkout would then see an empty cart and fail with 409 `cart_empty` instead of double-charging.
- `expectedTotal` is sent with checkout, so the user can only pay the total they saw. A mismatch returns 409 `cart_changed`, and the page reloads the cart.

## Retry behaviour
- No hidden automatic retries for writes. The client never silently re-sends a mutation. The user presses Retry (same key or order id), which keeps behaviour predictable and testable.
- Retry buttons appear only for retryable failures (network, timeout, 429, 5xx). Out-of-stock and validation errors do not offer Retry because repeating the same request cannot succeed.
- Reads (`useAsync`) show a Retry button on any failure except 404.
- 15 second timeout via `AbortController`. A timeout becomes `ApiError` with status 0 and code `timeout`.

## Error handling
- `ApiError` carries status, problem `code`, field `errors`, `failedLines`, `Retry-After` and the correlation id. `describeError` turns these into user text. Raw server detail is only shown for a few codes where the server message is the useful one.
- Correlation id: the client sends `X-Correlation-Id` on every request and prints it as "Reference" in error banners so support can find the request in logs.
- 412 and 428 show a conflict message with "Reload latest" in Admin. 409 `out_of_stock` lists failing lines and says the cart was kept. 429 shows the Retry-After seconds. 400 field errors render next to the field (`aria-invalid`, `aria-describedby`).
- Client-side validation (quantity 1 to 10, price greater than 0 with at most 2 decimals, whole-number stock, reason required) exists for speed only. The server stays authoritative and its errors are rendered the same way.

## Money and time
- One `formatRupees` (Intl, en-IN, INR, always 2 decimals). Amounts stay JSON numbers on the wire, as the contract says. The UI never does money arithmetic other than showing server totals, so floating point error cannot reach a displayed total.
- Timestamps arrive in UTC and are displayed with one `formatDateTime` in `Asia/Kolkata` (`DISPLAY_TIME_ZONE`). Order history date pickers are IST calendar days, converted to UTC instants: `from` is the start of the first day, `to` is the start of the day after the last day (exclusive). The conversion is DST-safe for other zones.

## Pagination
- The API only moves forward with an opaque token. "Previous" is a stack of tokens already used, so there is no offset and no unbounded page number. Changing a filter resets the stack.
- Product list shows `totalCount`. Orders and audit show only Newer/Older because the API gives no count for them.

## Accessibility
- Every control has a visible or visually hidden label. Skip link, landmarks (`header`, `nav aria-label="Main"`, `main`, `role="search"` form), table captions, `scope` on headers.
- Errors use `role="alert"`. Success and counts use `role="status"`. Stock state is text, not colour alone. Focus moves to the page `h1` on route change.
- Tested with axe on the product list, cart (with warning), checkout, admin editor and the app shell.
- Known gap: axe cannot check colour contrast in jsdom (no layout), so contrast is verified only by choosing dark text on light backgrounds. A real browser audit (Lighthouse or Playwright with axe) is the next step.

## Auth
- Dev only. A user switcher calls `POST /dev/token`. The token is kept in memory (client module) and mirrored to `sessionStorage` so a reload keeps the session. Trade-off: sessionStorage is readable by any script on the page (XSS). Acceptable for a dev token. In production I would use the OIDC authorization-code flow with PKCE and keep tokens in memory or in an HttpOnly cookie behind a BFF.
- No automatic token refresh. An expired stored token is discarded on load. A 401 shows "Please sign in".

## Deployment
- `Dockerfile` builds the bundle and serves it with nginx. `nginx.conf` proxies `/api/` to `api:8080`, so the browser stays same-origin and CORS is not needed. Hashed assets are cached for a year, `index.html` is never cached.

## Deliberately skipped
- No optimistic UI updates. Cart changes wait for the server so the user never sees a total the server did not confirm.
- No per-keystroke search. Search submits explicitly, which is friendlier to the search rate limit and to screen readers.
- No cart reservation or stock countdown (the backend does not reserve stock).
- No component library, no i18n framework, no E2E tests (Playwright would be the next addition).
- Admin lists the first page of products only (page size 20) with Next/Previous.

## Branding and visual design
- **Brand:** the store is branded as PowerPlay Store, with an original bat-and-ball mark (`public/brand/powerplay-logo.svg`, favicon, header). It deliberately does not copy IPL, BCCI or franchise logos or trade dress.
- **No false affiliation:** copy says "cricket merchandise for IPL fans" and the footer states the store is independent and not endorsed by the IPL, BCCI or any franchise. Team names appear only to describe products.
- **Visual design:** a clean, modern storefront that follows the system light or dark setting. Neutral surfaces, one warm accent, soft radii and short transitions, with reduced motion honoured. Product imagery is flat SVG illustrations tinted in the team's colours (`ProductVisual`), so there are no asset files, no licensing issues and no WebGL. The 3D viewer was removed to keep the page fast and the look calm (and three.js out of the bundle).

## Add to cart feedback
- **What the shopper sees:** the button turns green and reads "Added" for about two seconds, the header cart badge bumps and a "+N" floats up from it, and a confirmation card shows the product, the cart subtotal and View cart / Checkout links. Each product also shows "N in cart" from the cached cart.
- **One announcement, not three:** only the Add to cart control's `role="status"` line is announced ("Added MI Jersey to your cart."). The card is a labelled `aside` but not a live region, and the +N and badge are `aria-hidden`; the cart link's accessible name already says "Cart (2 items)". The button keeps "Add to cart" as its accessible name while it visually reads "Added", so voice control users can still target it.
- **Timing:** the card hides after 5 seconds, pauses while hovered or focused, and has a Dismiss button. It sits at the top right on desktop and at the bottom on phones so it never covers the header.
- **Source of truth:** the event comes from `CartContext.add`, using the server's returned cart, so the count and subtotal are the server's numbers, never client arithmetic.

## Shipping and totals
- The cart shows Items, Shipping and Total. Checkout sends `grandTotal` as `expectedTotal`, falling back to `total` for a server that predates shipping (`payableTotal`).

