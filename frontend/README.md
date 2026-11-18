# WordGame frontend

React + TypeScript + Vite, with React Router. See the [architecture](../docs/architecture.md) for gameplay authority and backend boundaries.

## Development

Use Node.js 24 and npm to match CI (minimum supported by the project: 22.22). See [local development setup](../docs/development-setup.md) for running both applications. From `frontend/`:

```sh
npm ci
npm run dev
```

On Windows PowerShell with script execution disabled, use `npm.cmd` instead of `npm`.

Copy `.env.example` to `.env.local` when configuring a backend. Set `VITE_API_BASE_URL` to its origin, for example `http://localhost:5000`, using the actual backend port. Leave it empty for same-origin requests. Restart Vite after changing environment values. `src/services/api/config.ts` supplies the normalized base URL to the centralized API client. The placeholder homepage still makes no backend requests. Vite embeds `VITE_` values in browser assets: they must never contain secrets. Local environment files are ignored by Git.

```sh
npm run typecheck
npm test
npm run lint
npm run format:check
npm run build
npm run preview
```

`build` includes TypeScript checks. `format` applies Prettier; ESLint handles code quality with React Hooks and Fast Refresh rules. Both application and Vite configuration use explicit strict TypeScript settings. TypeScript 5.9 satisfies both typescript-eslint and openapi-typescript peer ranges. `package-lock.json` pins dependency resolutions for `npm ci`.

Vitest runs API client and React component tests with `npm test`. Playwright runs desktop and mobile browser checks with `npm run test:e2e`, starting its own local frontend and API. See [automated testing](../docs/testing.md) for browser installation, suite commands, organization, and isolation.

## Shared API contracts

See [API contracts](../docs/api-contracts.md) for implemented endpoints, future specifications, versioning, and errors. All JSON requests go through `src/services/api/client.ts`; typed category operations are in `categories.ts`. Import shared generated types from `generated.ts` instead of declaring response interfaces in components.

With the Development backend running on port 5000, run `npm run api:generate` to refresh types and `npm run api:check` to detect drift. A trailing OpenAPI document URL overrides the default. Commit generated changes with backend DTO changes. No game/matchmaking client wrappers are added until their routes exist.

## Structure and boundaries

| Location            | Responsibility                                                        |
| ------------------- | --------------------------------------------------------------------- |
| `src/main.tsx`      | Browser entry point and router provider                               |
| `src/App.tsx`       | Route definitions: `/` and a not-found fallback                       |
| `src/pages/`        | Route-level presentation                                              |
| `src/services/api/` | Centralized HTTP client, category operations, and generated contracts |
| `src/styles/`       | Shared styles                                                         |
| `src/vite-env.d.ts` | Typed public environment variables                                    |

Add the following directories only when their first real implementation is needed:

- `src/features/{gameplay,lobby,matchmaking,profile}/`: feature-specific components, client state, and orchestration. Server gameplay rules remain on the backend.
- `src/components/ui/`: reusable, feature-independent presentation.
- `src/hooks/`: hooks shared by multiple features; keep feature-only hooks inside their feature.
- `src/types/`: shared contracts; keep local types near their feature.
- `src/utils/`: shared pure helpers.

Pages compose features and reusable UI. Components delegate networking to services, and client state reflects authoritative backend state. Avoid empty folders, speculative abstractions, or duplicate gameplay rules.

The homepage is intentionally a placeholder. Party lobbies, matchmaking, gameplay, and persistent state are later issues. No V1 frontend code is copied. Production static hosting must serve `index.html` for client routes so refreshes and direct links work with BrowserRouter.

See [coding standards](../docs/coding-standards.md#react-and-typescript) for component/service conventions and [CI](../docs/ci.md#dependency-policy) for the current development dependency limitation and its temporary exception.
