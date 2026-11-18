# Coding standards

These conventions apply to new and changed code. Follow [architecture](architecture.md) for layer boundaries and [CONTRIBUTING](../CONTRIBUTING.md) for issue/PR workflow. Formatters and lint configuration are the executable source of formatting rules; update documentation when those rules change.

## C#

- Use PascalCase for types, methods, properties, events, and enum members; prefix interfaces with `I`. Use camelCase for parameters, local variables, and private fields, matching the current codebase.
- Use file-scoped namespaces, explicit member accessibility, braces, four-space indentation, UTF-8, LF, and a final newline. Keep System imports first. `backend/.editorconfig` defines these rules, with two-space indentation for project/props/JSON files.
- Keep nullable reference types enabled. Validate external inputs and preserve domain invariants; do not use null suppression to hide an unexplained state. Keep public contracts explicit and avoid returning mutable internal session state.
- Use `Async` names for asynchronous methods, propagate `CancellationToken` through I/O, and avoid blocking async operations. Inject `TimeProvider` for time-dependent application behavior and tests.
- Keep business rules in Domain and orchestration/contracts in Application. Implement storage/providers in Infrastructure; keep HTTP DTOs, mapping, configuration, and dependency composition in API. Do not introduce ASP.NET Core or persistence dependencies into Domain.
- Use structured `ILogger` messages and the established Problem Details boundary. Do not log credentials, accepted-answer datasets, or private player data. Map public state deliberately instead of serializing domain/session objects directly.

Apply formatting with `dotnet format backend/WordGame.sln`; validate with the build and format commands in [testing](testing.md). Use small focused classes and existing interfaces rather than adding a service/repository layer with no responsibility.

## React and TypeScript

- Keep strict TypeScript, unchecked-index access checks, and exact optional property checks enabled. Use `import type` where appropriate, narrow unknown input, and avoid `any` or suppression comments that bypass a real contract problem.
- Use PascalCase component names and component filenames (`HomePage.tsx`), camelCase functions/variables, and `use` prefixes for hooks. Keep feature-only types/helpers near their use; move them to shared folders only for real reuse.
- Use function components and follow the React Hooks and Fast Refresh lint rules. Keep rendering separate from API calls and orchestration; components receive explicit props and render loading/error/empty states when applicable. Use semantic HTML, labelled controls, accessible interactions, and layouts that work on narrow screens.
- Keep pages responsible for composition and routing. Extract a reusable component when behavior/presentation is actually shared, with a small explicit API; do not build speculative component systems or oversized pages.
- Route JSON networking through `src/services/api/client.ts` and feature API wrappers. Use generated API types from `src/services/api/generated.ts`; regenerate them from Development OpenAPI after contract changes instead of editing generated output. See [API contracts](api-contracts.md).
- Keep server gameplay rules authoritative. Client validation and timers improve presentation but cannot determine scores, turn permissions, word validity, or match outcomes.
- Follow Prettier: two-space indentation, single quotes, no semicolons, and trailing commas. Frontend files use LF through `.gitattributes`. Run `npm run format` to apply formatting and `npm run lint` for code quality; do not hand-format generated contracts differently.

See the [frontend structure](../frontend/README.md#structure-and-boundaries) before creating shared folders. Prefer the existing router, fetch client, and React primitives over adding dependencies without a demonstrated need.

## Tests and dependencies

Test observable behavior, boundary cases, permissions, and failure paths appropriate to the change. Use isolated data/stores and controlled clocks; keep tests independent of production services. Component/browser tests should prefer accessible roles and labels. See [testing](testing.md) for suite responsibilities and filenames.

Keep dependencies minimal. Review compatibility, maintenance, security, and the lockfile before adding one. Document a necessary exception explicitly and follow [CI's dependency policy](ci.md#dependency-policy). Never silence a failing check or remove a useful test to make a change pass.
