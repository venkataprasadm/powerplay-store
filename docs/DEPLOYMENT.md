# Deployment (temporary demo)

| Part | Host | How it deploys |
|---|---|---|
| Web (React) | GitHub Pages at https://powerplaystore.venkataprasadm.com | `.github/workflows/pages.yml` on every push to `main` that touches `web/` |
| API (.NET) | Render free web service from `render.yaml` | Render deploys after the CI checks on `main` pass |
| Data | In-memory store, seeded at start | Resets whenever the API restarts or sleeps |

This is a demo setup, not production. Dev sign-in is on so the UI can switch between Asha, Ravi and Admin, which means anyone with the link can use the app.

## One-time setup
1. **API.** In Render choose New, Blueprint, pick this repository. It reads `render.yaml`. Note the service URL, for example `https://powerplay-store-api.onrender.com`.
2. **Repository variable.** In GitHub, Settings, Secrets and variables, Actions, Variables, add `API_BASE_URL` = `https://<your-service>.onrender.com/api/v1`.
3. **Pages.** Settings, Pages, Source: GitHub Actions. Custom domain: `powerplaystore.venkataprasadm.com`. Tick Enforce HTTPS once the certificate is ready.
4. **DNS.** At the domain registrar add a CNAME record: host `powerplaystore`, value `venkataprasadm.github.io`.
5. **CORS.** `render.yaml` already allows the Pages origin through `Cors__AllowedOrigins__0`. Change it if the domain changes.

## Pipeline
1. A push to `main` runs `ci.yml`: backend build and tests, web tests, optional Cosmos contract tests.
2. Render notices the green checks and rebuilds the API image.
3. `pages.yml` tests and builds the web app, publishes it to Pages, then smoke-tests the API catalogue endpoint with retries for a sleeping host.

## Known limits
- Free hosts sleep when idle and the first request can take about a minute. Open the site a few minutes before a demo.
- Data is in memory. A restart brings back the seeded catalogue only.
- Rate limits are per instance. Fine for one instance.
- For a real deployment use Azure Container Apps with Cosmos DB, managed identity, Key Vault and a real identity provider. See DESIGN.md.
