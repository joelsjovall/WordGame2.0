# Shared API contracts

F009 establishes the transport boundary for both game modes. C# DTOs live in `WordGame.Api/Contracts/V1`; Domain and Application models are not request/response bodies. Mapping code projects only public state. TypeScript contracts are generated from backend OpenAPI rather than maintained as parallel handwritten interfaces.

## Naming, versions, and representations

- Product resources use `/api/v1`, plural lowercase resource names, standard HTTP methods, and camelCase JSON properties. Breaking changes require a new major URL/document version. Compatible additions stay in v1.
- GUIDs are UUID strings. Category IDs are opaque strings; route IDs are URL-encoded by the frontend wrapper. IDs/lobby codes identify resources and do not authenticate a caller.
- Enums are lower-camel-case strings. Numeric enum values and quoted numeric fields are not accepted by the JSON serializer.
- Session revisions and expected revisions are **decimal strings**, preserving .NET Int64 precision in JavaScript. Compare them as opaque values for equality or as BigInt for ordering, rather than using Number or lexicographic order. Future action handlers must require nonnegative Int64 values in canonical decimal form and reject malformed/out-of-range values.
- Timestamps use RFC 3339 date-time strings. The future game handler supplies serverTime from TimeProvider; challenge deadlines remain server-owned.
- Object responses are direct DTOs. Collections use `{ "items": [...] }`. Phase-dependent state fields are present with null when unavailable. There is no redundant success/error envelope.
- Health remains the unversioned operational `GET /health`, returning plain text. It is separate from the JSON product client.

Future DTO deserialization requires constructor fields, respects nullable annotations, rejects unknown members, and rejects unknown action discriminators. Structural deserialization is not business validation: future handlers must validate nonempty UUIDs, bounds, lobby membership/readiness, permissions, game phases, and revision/idempotency rules before invoking gameplay.

## Implemented endpoints

| Method and path | Response |
| --- | --- |
| GET /api/v1/categories | 200 CategoryListResponse, sorted metadata only |
| GET /api/v1/categories?difficulty=easy | 200 filtered metadata; easy/medium/hard are the exact allowed values |
| GET /api/v1/categories/{categoryId} | 200 CategoryResponse or 404 Problem Details |

CategoryResponse contains only id, name, and difficulty. Accepted answers, aliases, canonical answer IDs, and internal indexes are never returned. Invalid/empty/numeric/repeated difficulty parameters produce 400 with a field error. Unknown product routes produce 404 Problem Details.

These metadata reads are public. Protected game/lobby/matchmaking routes are not mapped until identity and the corresponding workflows exist. There are no dummy session endpoints, matchmaking handlers, or gameplay POST routes.

## Specified future workflows

The following describes the reserved contract intent, not available endpoints:

| Operation | Planned method/path | Contract and authority |
| --- | --- | --- |
| Create a Party game from a ready lobby | POST /api/v1/games | CreateGameRequest: lobbyId. A future authenticated host handler derives the fixed roster/settings from the lobby and returns 201 GameCreatedResponse with gameId and a Location header. Clients do not nominate arbitrary participants. |
| Join a pregame Party lobby | POST /api/v1/lobbies/join | JoinGameRequest: lobbyCode. JoinGameResponse: lobbyId, lobbyCode, and nullable gameId. A lobby can contain one host before other players arrive; it is distinct from F008's fixed-roster gameplay session. |
| Read authoritative state | GET /api/v1/games/{gameId}/state | GameStateResponse, after verifying player membership. |
| Submit an action | POST /api/v1/games/{gameId}/actions | GameActionRequest discriminated union; returns GameActionResponse with actionId and the committed version. |
| Observe invalidations | GET /api/v1/games/{gameId}/events | Future authenticated SSE transport using GameEventResponse. Clients retrieve a new state snapshot; events are not deltas. |

CreateGameRequest applies to a ready Party lobby. Online matchmaking creates games internally after selecting opponents. Both routes into play use the same GameStateResponse, action contracts, event shape, session store, and future game engine; there are no duplicated mode-specific gameplay DTOs.

Pregame lobby creation/settings, guest credential transport, and a complete lobby response will be finalized in their own issues. Identity comes from a verified server-issued credential, not playerId/hostId fields supplied in a gameplay body. Merely possessing a lobby code does not grant identity or host rights.

### Actions and state

Each action includes a caller-generated UUID actionId and string expectedVersion, with a `type` discriminator:

- selectCategory adds categoryId.
- placeBid adds positive wordCount.
- callBluff has no client-chosen bidder, count, or deadline.
- submitWord adds word.

No action accepts score changes, validation results, authoritative timestamps, or an acting player ID. Future handlers derive the actor from identity, compare expectedVersion, serialize the operation through the session store, and use Domain guards. Stale state is 409. Reusing an actionId with the same payload should return its original outcome; reusing it for a different payload is a conflict. F014's [application turn layer](turn-management.md) now checks revisions and prevents reused IDs from advancing state. Original-response receipt replay and HTTP action execution remain future adapter work; no action endpoint is mapped here.

GameStateResponse includes session status, revision, server time, public player identity/name/score/presence, and a nullable current round. A round exposes its phase, active player, selected category metadata, highest bid, and nullable challenge. Challenge responses contain role IDs, required count, timestamps, outcome, accepted count, and submitted text/status. They contain no full answer datasets or persistence entities. GameStateMapper projects detached F008 snapshots without mutating them.

### Matchmaking specification

Future Online Mode can use POST /api/v1/matchmaking/searches to enter a search, GET /api/v1/matchmaking/searches/{searchId} for status, and DELETE on that resource to cancel (204). The authenticated identity is implicit; a future response should include searchId, a searching/matched/cancelled/expired state, and gameId only once matched. F033-F035 will finalize acceptance, expiration, and rematch details before mapping routes or generating matchmaking DTOs. No placeholder implementation is added here.

## Errors

Product errors use `application/problem+json`, with RFC 9457 fields type, title, status, detail/instance where applicable, plus traceId and optional errors:

```json
{
  "type": "about:blank",
  "title": "Invalid request.",
  "status": 400,
  "instance": "/api/v1/categories",
  "traceId": "server-generated-correlation-id",
  "errors": { "difficulty": ["Use easy, medium, or hard exactly once."] }
}
```

400 means invalid input; future identity/membership checks use 401/403; 404 means absent resource; 409 means state/revision conflict; 500 is an unexpected server failure. Unexpected exceptions are logged server-side and use a generic public title. Stack traces, exception messages, request bodies, and credentials are not included in the response. Error instance excludes the query string.

ApiProblems centralizes current validation/status errors and the exception handler's response shape. The frontend centralizes HTTP failures as ApiError, preserving status, traceId, and field errors. Non-JSON/empty proxy errors use a generic status-based message. Network errors, aborts, and invalid JSON on successful responses remain transport/protocol failures. The client never retries requests automatically.

## OpenAPI and TypeScript generation

Development hosts expose the OpenAPI JSON document at `http://localhost:5000/openapi/v1.json`. It is not exposed by default in Production. The document includes only implemented paths, plus explicitly registered schemas for future DTOs. Action variants are represented as unions with literal required type fields; phase-dependent nullable fields are explicitly annotated for standalone schema generation.

With the Development API running, run from `frontend/`:

```sh
npm run api:generate
npm run api:check
```

Pass a different document URL as a trailing argument if needed, for example `npm run api:generate -- http://localhost:5080/openapi/v1.json`. The same applies to api:check. The generator runs openapi-typescript, formats its output using project Prettier configuration, and writes `src/services/api/generated.ts`. It validates references and fails if the document cannot be fetched. The check command fails on stale types; it requires a running current backend and is not a substitute for rebuilding it.

Commit generated changes with the C# contract change. Do not edit generated types manually. Generated code is excluded from ESLint but checked by TypeScript, formatting, and regeneration. LF is fixed for the generated file so checks are portable across Git newline settings.

UUIDs, timestamps, and revisions remain strings in generated TypeScript; generation does not construct Date objects, parse revisions into numbers, or rename JSON fields.

Use `createApiClient`/`apiClient` for JSON transport and feature service wrappers such as `categoriesApi` for typed operations. They use VITE_API_BASE_URL (empty means same-origin), encode path/query values, preserve request options and AbortSignal, and share error handling. Components must not duplicate fetch logic or API type declarations. Request/response wrappers import `components`/`operations` from generated.ts. Success types are compile-time contracts; no runtime response-schema validator is included.

The client defaults to fetch's same-origin credentials behavior. A future guest/auth implementation must configure credentials centrally once the cookie/header and CORS policy is decided; no token storage or fake authentication is introduced here.

## Validation and remaining scope

Backend tests cover metadata privacy/filtering, Problem Details, route availability, development OpenAPI, schema references/nullability, strict action serialization, and the same state projection for 2/4 players. Frontend tests cover JSON transport, headers, cancellation, encoding, error handling, safe relative paths, and no retries. Live smoke checks exercise the actual frontend client against category success, 400, and 404 responses.

Run backend build/tests/format checks and frontend test/lint/typecheck/build/format checks. Regenerate/check types against the rebuilt Development API whenever contracts change. F009 does not implement identity, lobby joining/creation, game action execution, public state/SSE routes, matchmaking, or complete request semantic validation.

Implementation references: [ASP.NET Core OpenAPI customization](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0), [openapi-typescript tooling](https://openapi-ts.dev/cli).
