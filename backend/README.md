# WordGame backend

ASP.NET Core on .NET 10 (LTS). Install a serviced .NET 10 SDK, then run from the repository root:

```sh
dotnet restore backend/WordGame.sln
dotnet build backend/WordGame.sln --no-restore
dotnet test backend/WordGame.sln --no-build
dotnet format backend/WordGame.sln --verify-no-changes --no-restore
dotnet run --project backend/WordGame.Api --launch-profile http
```

The HTTP profile serves `http://localhost:5000`. `GET /health` returns HTTP 200 and `Healthy` with no database or external service required. This is a basic process health check, not a guarantee of future provider or storage readiness. The optional `https` profile also serves `https://localhost:7001` and requires a trusted development certificate (`dotnet dev-certs https --trust`).

Set the frontend's `VITE_API_BASE_URL=http://localhost:5000` in `frontend/.env.local` for local HTTP development. No gameplay endpoints or frontend API calls are introduced here.

## Project boundaries

| Project | References | Responsibility |
| --- | --- | --- |
| `WordGame.Domain` | None | Shared entities, lifecycle guards, turns, scores, and challenge models |
| `WordGame.Application` | Domain | Validation, session lifecycle, and provider/store contracts |
| `WordGame.Infrastructure` | Application | JSON category loading/caching and the in-memory session store |
| `WordGame.Api` | Application, Infrastructure | HTTP endpoints, middleware, configuration, composition root |
| `WordGame.Api.Tests` | API | xUnit HTTP integration tests using WebApplicationFactory |
| `WordGame.Domain.Tests` | Domain | xUnit model and invariant tests without an API host |
| `WordGame.Application.Tests` | Application | xUnit word validation and provider-boundary tests |
| `WordGame.Infrastructure.Tests` | Infrastructure | xUnit JSON loading/cache and session concurrency/cleanup tests |

The Domain remains independent of ASP.NET Core and infrastructure; see [domain models](../docs/domain-models.md) for relationships and [category validation](../docs/category-validation.md) for validation rules. The API registers the stateless word validator and a JSON category provider through built-in dependency injection. The provider loads and validates all datasets before traffic is accepted, with cached reads afterward. See [dataset setup](../docs/category-datasets.md) to add categories or configure `Categories:Directory`. No database or extra runtime package is needed.

The API also registers the shared in-memory session store and automatic cleanup worker. See [session management](../docs/session-management.md) for isolation, lifecycle, reconnection, change notifications, and `Sessions` retention settings. Active sessions are lost on restart; this store supports one backend instance.

## Configuration and conventions

- Nullable reference types, implicit usings, and build-time code style enforcement are set in `Directory.Build.props`. `.editorconfig` defines formatting, file-scoped namespaces, PascalCase types and members, and the `I` prefix for interfaces. Run `dotnet format` to apply these conventions.
- Logging explicitly uses the built-in console/debug providers and structured `ILogger` messages, without depending on Windows Event Log permissions. Levels are configured in `appsettings.json` and `appsettings.Development.json`. The centralized handler logs unexpected exceptions with a trace ID for correlation.
- Unexpected request exceptions return HTTP 500 Problem Details with a generic title and trace ID; exception messages and stack traces stay out of responses, including in Development. Domain-specific error mapping is future gameplay work.
- CORS is enabled only in Development, for the exact origins `http://localhost:5173` and `http://127.0.0.1:5173`. These are configured under `Cors:AllowedOrigins` in `appsettings.Development.json`; change the list if the Vite port changes. Credentials are not enabled. CORS does not authenticate players. Production cross-origin policy must be explicitly designed when deploying.
- Configuration uses ASP.NET Core defaults (JSON, environment variables, command-line arguments). For example, `Cors__AllowedOrigins__0` overrides the first development origin. Keep secrets out of tracked files; future sensitive values belong in environment variables or user secrets.
- HTTPS termination, identity, SSE, gameplay, category providers, storage, and production deployment are outside F003.

Tests cover database-free startup, health responses, allowed development preflights, rejected origins, production CORS behavior, safe unexpected-error responses, and absence of demo endpoints.
