# Automated testing

Install a serviced .NET 10 SDK and Node.js 22.22+ with npm. Run backend commands from the repository root and frontend commands from `frontend/`. On PowerShell with script execution disabled, use `npm.cmd`.

## Backend

```sh
dotnet build backend/WordGame.sln --configuration Release --warnaserror
dotnet test backend/WordGame.sln --configuration Release --no-build
dotnet format backend/WordGame.sln --verify-no-changes --no-restore
```

The build restores NuGet dependencies. Tests require no running API, database, credentials, or production services.

PowerShell users can run `./scripts/test-backend.ps1` to build and test everything. Use `-Suite Unit` for Domain, Application, and Infrastructure tests, or `-Suite Integration` for API tests. If local script execution is disabled, use the equivalent .NET commands above. To run integration tests alone:

```sh
dotnet test backend/WordGame.Api.Tests/WordGame.Api.Tests.csproj --configuration Release
```

| Location | Responsibility |
| --- | --- |
| `backend/WordGame.Domain.Tests` | Gameplay entities, invariants, turns, and scoring |
| `backend/WordGame.Application.Tests` | Application services and provider boundaries |
| `backend/WordGame.Infrastructure.Tests` | JSON datasets, isolated temporary files, session concurrency and cleanup |
| `backend/WordGame.Api.Tests` | HTTP integration tests, health, CORS, errors, contracts, and composition |

The existing xUnit projects are reused rather than duplicated under a second test directory. `WordGameApiFactory` centralizes `WebApplicationFactory<Program>` setup. Each instance owns its host and in-memory store. Tests can override services and configuration through `WithWebHostBuilder`. The `Production` environment name checks deployment behavior in an in-process test host; it does not connect to production infrastructure.

## Frontend unit and component tests

```sh
npm ci
npm test
npm run test:components
npm run test:api
npm run test:watch
```

Vitest runs API client tests in Node with injected fake fetch responses. React Testing Library, user-event, and jest-dom exercise rendering and navigation in jsdom. `src/App.test.tsx` opts into jsdom; new component tests should do the same. Shared setup cleans the rendered DOM after each test. Component tests require no backend.

Colocate feature/component tests as `src/**/*.test.tsx`; use `tests/**/*.test.ts` for transport and shared test helpers. The Vitest include patterns exclude Playwright specs. Strict TypeScript checks include test files.

## Browser tests

```sh
npm exec playwright install chromium
npm run test:e2e
```

On Linux CI, install browser system dependencies with `npm exec playwright install --with-deps chromium`. Browser downloads and package restores need internet access; test execution uses local resources.

Playwright owns dedicated servers at `http://127.0.0.1:5175` (Vite) and `http://127.0.0.1:5085` (API), waits for readiness, and stops them afterward. Leave those ports free; existing servers are deliberately not reused. The API loads repository JSON datasets and uses in-memory sessions. Configuration fixes the local API URL and development CORS origins without reading production credentials.

`frontend/e2e/smoke.spec.ts` checks homepage rendering, runtime errors, horizontal overflow, unknown-route recovery and refresh, backend health, and public category metadata. Tests run in desktop Chromium and Chromium with Pixel 5 emulation. Emulation does not replace testing on physical devices or other browser engines.

Use `npm run test:e2e:ui` for interactive debugging or `npm exec playwright show-report` after a run. Reports, results, coverage, and failure traces/screenshots are ignored by Git. CI rejects focused tests, limits workers, and retries failures twice. See [continuous integration](ci.md) for automated runs and report artifacts.

## Extending the suites

Add gameplay rules to Domain tests, orchestration to Application tests, and HTTP identity/session behavior to API tests. Group new files by feature when suites grow. Use isolated stores, test data, and controllable clocks rather than production resources or timing sleeps. Future multiplayer browser journeys can create separate browser contexts for distinct players against the same local API; no multiplayer workflows are asserted before they exist.

Before submitting changes, run the relevant suites plus `npm run lint`, `npm run format:check`, `npm run build`, and backend formatting/build checks. Keep accessibility-based browser/component selectors where possible.

Known tooling limitation at F010 verification: npm audit reports 12 high-severity development dependency findings from the same transitive `picomatch` advisory (GHSA-fc5q-39xc-vjpf) recorded during frontend setup. Recheck for a patched release; do not force incompatible toolchain downgrades.
