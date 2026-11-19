# WordGame 2.0

A multiplayer word-bidding and bluffing game for mobile and desktop browsers. Players choose categories, bid on how many valid words they can name, raise bids or call bluffs, and attempt timed word challenges.

## Game modes and current status

- **Party Mode:** planned private lobbies for 2–4 players, lobby codes/invitation links, host settings, readiness, reconnection, and rematches.
- **Online Multiplayer:** planned random 1v1 matchmaking with guest identities, automatic matches, disconnect/forfeit handling, and rematches. Persistent statistics will follow when storage is introduced; ranked play is optional.

Both modes will use the same server-authoritative gameplay engine. This repository currently provides domain models, the shared [game-engine lifecycle](docs/game-engine.md), word validation, local category datasets, an in-memory session store, category metadata API contracts, guest identity, private lobby creation/joining APIs, protected game state/SSE endpoints, lobby creation/joining screens with live host settings and match startup, automated tests, and CI. Playable gameplay controls, readiness/player management, and matchmaking remain planned. See [project progress](docs/project-context.md) for the issue handoff.

## Stack and structure

| Location                             | Technology and responsibility                                                        |
| ------------------------------------ | ------------------------------------------------------------------------------------ |
| `frontend/`                          | React, strict TypeScript, Vite, and React Router; presentation and API communication |
| `backend/WordGame.Domain`            | C# models and gameplay invariants, independent of HTTP and storage                   |
| `backend/WordGame.Application`       | Application services, session lifecycle, and provider/store contracts                |
| `backend/WordGame.Infrastructure`    | JSON category provider and in-memory session store                                   |
| `backend/WordGame.Api`               | ASP.NET Core on .NET 10; HTTP contracts, endpoints, configuration, and composition   |
| `backend/*.Tests`, `frontend/` tests | xUnit unit/integration tests, Vitest/React Testing Library, and Playwright           |
| `.github/workflows/`                 | GitHub Actions builds, tests, and dependency/secret scanning                         |
| `docs/`                              | Architecture, setup, conventions, contracts, and feature decisions                   |

SSE is the implemented game-state notification transport. Active sessions currently support one backend instance and are lost on restart. There is no database requirement; persistence will be added for statistics and match history when needed. See [architecture](docs/architecture.md) for boundaries and decisions.

## Local development

Install a serviced .NET 10 SDK and Node.js 24 with npm (the frontend minimum is 22.22). Clone the repository, then use two terminals:

```sh
# Terminal 1: repository root
dotnet restore backend/WordGame.sln
dotnet run --project backend/WordGame.Api --launch-profile http
```

```sh
# Terminal 2: repository root
cd frontend
npm ci
npm run dev
```

The API serves `http://localhost:5000`; Vite normally serves `http://localhost:5173`. The homepage renders without an API connection; creating/joining lobbies and configuring matches require the backend. Before using API-backed features, set the public `VITE_API_BASE_URL` in `frontend/.env.local` as described in [development setup](docs/development-setup.md). Use `npm.cmd` on PowerShell if script execution blocks `npm`.

## Build, test, and contribute

See [automated testing](docs/testing.md) for backend/frontend builds, checks, suite commands, and browser installation. [CI](docs/ci.md) runs the same checks on pull requests and `main`, with test reports and security scans.

Start with [CONTRIBUTING.md](CONTRIBUTING.md) before implementing an issue. Work proceeds one issue at a time across [six milestones](docs/project-context.md#roadmap). Follow [coding standards](docs/coding-standards.md), read the complete issue and its acceptance criteria, and keep documentation aligned with changes. AI assistants must also follow [AGENTS.md](AGENTS.md).

Useful references: [frontend](frontend/README.md), [backend](backend/README.md), [API contracts](docs/api-contracts.md), and [category datasets](docs/category-datasets.md). The [original V1 repository](https://github.com/joelsjovall/wordgame) is reference material only; reuse requires review and tests.

Never commit credentials or put secrets in browser configuration. Current dependency scan exceptions are documented in [CI](docs/ci.md); the outstanding V1 credential-rotation follow-up is tracked in [project context](docs/project-context.md).
