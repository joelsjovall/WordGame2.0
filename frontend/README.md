# WordGame frontend

React + TypeScript + Vite, with React Router. See the [architecture](../docs/architecture.md) for gameplay authority and backend boundaries.

## Development

Use Node.js 22.22+ and npm. From `frontend/`:

```sh
npm ci
npm run dev
```

On Windows PowerShell with script execution disabled, use `npm.cmd` instead of `npm`.

Copy `.env.example` to `.env.local` when configuring a backend. Set `VITE_API_BASE_URL` to its origin, for example `https://localhost:7001`, using the actual backend port. Leave it empty for same-origin requests. Restart Vite after changing environment values. `src/services/api/config.ts` exposes the normalized base URL for future API clients; no backend requests are made yet. Vite embeds `VITE_` values in browser assets: they must never contain secrets. Local environment files are ignored by Git.

```sh
npm run typecheck
npm run lint
npm run format:check
npm run build
npm run preview
```

`build` includes TypeScript checks. `format` applies Prettier; ESLint handles code quality with React Hooks and Fast Refresh rules. Both application and Vite configuration use explicit strict TypeScript settings. TypeScript 6 is used for compatibility with typescript-eslint's supported peer range. `package-lock.json` pins dependency resolutions for `npm ci`.

## Structure and boundaries

| Location            | Responsibility                                         |
| ------------------- | ------------------------------------------------------ |
| `src/main.tsx`      | Browser entry point and router provider                |
| `src/App.tsx`       | Route definitions: `/` and a not-found fallback        |
| `src/pages/`        | Route-level presentation                               |
| `src/services/api/` | Backend API configuration and future transport clients |
| `src/styles/`       | Shared styles                                          |
| `src/vite-env.d.ts` | Typed public environment variables                     |

Add the following directories only when their first real implementation is needed:

- `src/features/{gameplay,lobby,matchmaking,profile}/`: feature-specific components, client state, and orchestration. Server gameplay rules remain on the backend.
- `src/components/ui/`: reusable, feature-independent presentation.
- `src/hooks/`: hooks shared by multiple features; keep feature-only hooks inside their feature.
- `src/types/`: shared contracts; keep local types near their feature.
- `src/utils/`: shared pure helpers.

Pages compose features and reusable UI. Components delegate networking to services, and client state reflects authoritative backend state. Avoid empty folders, speculative abstractions, or duplicate gameplay rules.

The homepage is intentionally a placeholder. Party lobbies, matchmaking, gameplay, and persistent state are later issues. No V1 frontend code is copied. Production static hosting must serve `index.html` for client routes so refreshes and direct links work with BrowserRouter.

## Initial verification

F002 was checked using Node 25.2.1 and npm 11.13.0. ESLint, strict TypeScript, formatting, and production build checks passed. A temporary Playwright smoke check in headless Edge verified homepage rendering, unknown-route handling, return navigation, browser history, mobile overflow, the default API URL, and absence of runtime errors. Browser test tooling was installed outside the repository; a permanent frontend test suite is not introduced by this setup.

Known dependency limitation: the initial npm audit reports 11 high-severity development-tool findings traced to the transitive `picomatch` dependency (GHSA-fc5q-39xc-vjpf). The latest registry version available during setup, 4.0.7, is affected. Recheck when a patched release is available; avoid forced toolchain downgrades. This is separate from the outstanding V1 credential rotation follow-up.
