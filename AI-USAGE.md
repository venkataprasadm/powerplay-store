# AI usage

## Tools I used
- **Claude Code (Claude desktop app)** with Claude Fable 5.1, then Claude Sonnet 5.5. I started it with a single prompt that attached the assessment PDF and asked for the project to be built.
- **A Claude subagent** (a fork of the same session) built the React front end in parallel, working from the API contract in `docs/API.md`.
- **Claude Code with Claude Opus 5.5** on 5 November 2026 for the UI redesign and gap review (see the last row of the table below).
- I did not use any other AI tools.

## Where AI helped
| Area | What AI produced for me |
|---|---|
| Requirements | Read the assessment PDF and turned it into a checklist and the API contract. |
| Architecture | Layering, the choice of partition key, the checkout algorithm, and the trade-off write-ups in `DESIGN.md`. |
| Backend code | Domain, application services, Cosmos and in-memory stores, API, middleware, error mapping, rate limiting, auth. |
| Tests | Backend tests (domain, concurrency, HTTP, storage contract) and front end tests. |
| Front end | React app, typed client, idempotency hook, accessibility, `web/DECISIONS.md`. |
| Delivery | Dockerfiles, compose file, CI workflow, Cosmos creation script. |
| Bugs found while writing tests | Two races: a concurrent retry of the same order wrongly reporting out of stock, and a retry after the cart was cleared wrongly reporting an empty cart. Both are fixed and covered by tests. |
| UI redesign and gap review (5 November 2026) | Replaced the 3D viewer with flat SVG product images and a light/dark theme, compared the brief with the code, and added threshold shipping in the cart, detection of reused idempotency keys, cart line display fields, add to cart feedback (+N badge, confirmation card), a page size control and the cart/checkout/orders layout, with tests and doc updates. |

## What I decided
- I chose which assessment to take on. The scope came from the brief.
- I chose port 5190 for the API because 5080 was already in use on my machine.

## How I verified it
The checks ran inside the Claude Code sessions: `dotnet test` for the backend, and `npm test`, `tsc` and `npm run build` for the front end. "Verification status" in `DESIGN.md` lists exactly what was and was not run.

## My own review
- What I read line by line and can defend: _to add_
- What I changed or rewrote after generation: _to add_
- What I would do differently: _to add_
