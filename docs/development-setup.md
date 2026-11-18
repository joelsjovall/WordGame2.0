# Local development setup

## Prerequisites

- Git and a serviced **.NET 10 SDK** (a runtime alone cannot build the solution).
- **Node.js 24** with npm, matching CI. The frontend declares a minimum of Node 22.22.
- An editor supporting C# and TypeScript; no editor extension is required to run the CLI commands.

No database, registration provider, production account, Docker, or production credential is required. Package restores and Playwright browser installation need network access.

```sh
git clone https://github.com/joelsjovall/WordGame2.0.git
cd WordGame2.0
dotnet --version
node --version
npm --version
```

On Windows PowerShell with script execution disabled, use `npm.cmd` in place of `npm`; changing the machine execution policy is unnecessary.

## Run the backend

From the repository root:

```sh
dotnet restore backend/WordGame.sln
dotnet run --project backend/WordGame.Api --launch-profile http
```

The `http` launch profile selects Development and binds `http://localhost:5000` without opening a browser. Visit `/health` for `Healthy`, `/api/v1/categories` for public category metadata, and `/openapi/v1.json` for the Development-only API description. The JSON datasets are copied into the API output and validated on startup; no manual data import is needed.

The optional `https` profile also binds port 7001 and requires a trusted development certificate; see the [backend guide](../backend/README.md). Stop the server with Ctrl+C. Active in-memory sessions disappear on restart.

## Run the frontend

In a second terminal, from the repository root:

```sh
cd frontend
npm ci
npm run dev
```

Open the URL printed by Vite, normally `http://localhost:5173`. The current homepage is a placeholder and makes no API calls. Configure API communication before testing API-backed features:

1. Copy `frontend/.env.example` to `frontend/.env.local` (PowerShell: `Copy-Item frontend/.env.example frontend/.env.local`, from the repository root).
2. Set `VITE_API_BASE_URL=http://localhost:5000` in the local file.
3. Restart Vite after changing values.

There is no configured Vite API proxy. An empty API base uses the frontend origin, so direct API calls need either the explicit backend URL above or a same-origin hosting arrangement. Backend Development CORS allows `http://localhost:5173` and `http://127.0.0.1:5173`. If Vite chooses another port, free port 5173 or update the backend's allowed origins and restart it. See [backend configuration](../backend/README.md#configuration-and-conventions).

## Build and verify

Run from the repository root:

```sh
dotnet build backend/WordGame.sln --configuration Release --warnaserror
dotnet test backend/WordGame.sln --configuration Release --no-build
dotnet format backend/WordGame.sln --verify-no-changes --no-restore
```

Run from `frontend/`:

```sh
npm run typecheck
npm run lint
npm run format:check
npm test
npm run build
```

Use [automated testing](testing.md) for suite filtering, Playwright installation and commands, and test isolation. Browser tests manage their own servers on ports 5175 and 5085; do not start development servers on those ports. `npm run preview` serves the frontend build locally and does not start the API. For generated contracts, keep the Development API running and use `npm run api:check` or `npm run api:generate` from `frontend/`.

## Configuration and secrets

Frontend `VITE_` values are public and embedded in browser assets. They must never contain credentials, signing keys, private connection strings, or server-only settings. Frontend local environment files are ignored except for the safe `.env.example`; commit example names and public values only.

ASP.NET Core reads its standard JSON, environment, and command-line configuration. `appsettings.json`, `appsettings.Development.json`, and launch profiles contain non-secret defaults. Use environment variables for local overrides, for example `Cors__AllowedOrigins__0` or `Categories__Directory`. Future secrets belong in environment variables or explicitly configured .NET user secrets, never tracked JSON. `appsettings.Local.json` is ignored but is **not automatically loaded** by the current program. A `.env` file is not automatically loaded by the backend either.

Keep server-only settings out of frontend code, use test/local resources rather than production services, and review staged diffs before committing. Do not include secrets in logs, screenshots, fixtures, issues, or PRs. If a credential is exposed, remove it from use and rotate/revoke it; deleting the file does not invalidate it. The previously exposed V1 credential must never be reused or reproduced, and its rotation remains unverified in [project context](project-context.md).

See [CI](ci.md) for dependency auditing, redacted secret scanning, known exceptions, and artifact handling. CI scans do not replace reviewing configuration or verifying credential rotation.
