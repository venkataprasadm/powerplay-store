# Contributing

## Workflow
- Branch from `main` using `feat/...`, `fix/...` or `docs/...`.
- Use Conventional Commits, for example `feat(cart): reject quantity above 10`.
- Open a pull request. CI must be green and the template completed.

## Local checks
```bash
dotnet build && dotnet test
cd web && npm ci && npm run lint --if-present && npm test && npm run build
```

## Decisions
Architecture decisions live in [DESIGN.md](DESIGN.md). Add or amend a decision record when a change alters one.
