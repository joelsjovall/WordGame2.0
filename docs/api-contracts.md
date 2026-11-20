# Shared API contracts

F009 establishes the transport boundary for both game modes. C# DTOs live in `WordGame.Api/Contracts/V1`; Domain and Application models are not request/response bodies. Mapping code projects only public state. TypeScript contracts are generated from backend OpenAPI rather than maintained as parallel handwritten interfaces.

## Naming, versions, and representations

- Product resources use `/api/v1`, plural lowercase resource names, standard HTTP methods, and camelCase JSON properties. Breaking changes require a new major URL/document version. Compatible additions stay in v1.
- GUIDs are UUID strings. Category IDs are opaque strings; route IDs are URL-encoded by the frontend wrapper. IDs/lobby codes identify resources and do not authenticate a caller.
- Enums are lower-camel-case strings. Numeric enum values and quoted numeric fields are not accepted by the JSON serializer.
- Session revisions and expected revisions are **decimal strings**, preserving .NET Int64 precision in JavaScript. Compare them as opaque values for equality or as BigInt for ordering, rather than using Number or lexicographic order. Future action handlers must require nonnegative Int64 values in canonical decimal form and reject malformed/out-of-range values.
- Timestamps use RFC 3339 date-time strings. The game state handler supplies serverTime from TimeProvider; challenge deadlines remain server-owned.
- Object responses are direct DTOs. Collections use `{ "items": [...] }`. Phase-dependent state fields are present with null when unavailable. There is no redundant success/error envelope.
- Health remains the unversioned operational `GET /health`, returning plain text. It is separate from the JSON product client.

DTO deserialization requires constructor fields, respects nullable annotations, rejects unknown members, and rejects unknown action discriminators. Structural deserialization is not business validation: handlers validate nonempty UUIDs, bounds, lobby membership/readiness, permissions, game phases, and revision/idempotency rules before invoking gameplay.

## Implemented endpoints

| Method and path | Response |
| --- | --- |
| GET /api/v1/categories | 200 CategoryListResponse, sorted metadata only |
| GET /api/v1/categories?difficulty=easy | 200 filtered metadata; easy/medium/hard are the exact allowed values |
| GET /api/v1/categories/{categoryId} | 200 CategoryResponse or 404 Problem Details |
| GET /api/v1/games/{gameId}/state | 200 GameStateResponse; protected cookie and roster membership required |
| GET /api/v1/games/{gameId}/events | 200 text/event-stream GameEventResponse invalidations; same authorization |
| POST /api/v1/guests | JSON {} ensures a server-issued guest identity; 200 GuestIdentityResponse and a protected cookie when needed |
| POST /api/v1/lobbies | CreateLobbyRequest requires the cookie; 201 LobbyResponse, 200 replay, 400 invalid input, or 409 request conflict |
| POST /api/v1/lobbies/join | JoinLobbyRequest: lobbyCode, displayName, optional lobbyId; cookie required; 200 LobbyResponse, 400/401/403/404/409 errors |
| GET /api/v1/lobbies/{lobbyId} | Cookie and membership required; 200 LobbyResponse or 401/404 |
| GET /api/v1/lobbies/{lobbyId}/events | Protected SSE lobby snapshots |
| POST /api/v1/lobbies/{lobbyId}/settings | Host-only UpdateLobbySettingsRequest with expectedVersion; 200 LobbyResponse or 400/401/403/404/409 |
| POST /api/v1/lobbies/{lobbyId}/start | Host-only StartLobbyRequest with expectedVersion; 200 LobbyResponse with gameId or 400/401/403/404/409 |
| POST /api/v1/lobbies/{lobbyId}/ready | Member-only SetLobbyReadyRequest with expectedVersion and isReady; 200 LobbyResponse or 400/401/403/404/409 |
| POST /api/v1/lobbies/{lobbyId}/leave | Member-only JSON {}; 200 updated/closed LobbyResponse or 400/401/403/404/409 |
| POST /api/v1/lobbies/{lobbyId}/remove | Host-only RemoveLobbyPlayerRequest with target playerId; 200 LobbyResponse or 400/401/403/404/409 |
| POST /api/v1/lobbies/{lobbyId}/rematch | Host-only current expectedVersion; 200 waiting LobbyResponse after an ended match |
| POST /api/v1/games/{gameId}/actions | Protected GameActionRequest union; 200 GameActionResponse or 400/401/403/404/409 |

CategoryResponse contains only id, name, and difficulty. Accepted answers, aliases, canonical answer IDs, and internal indexes are never returned. Invalid/empty/numeric/repeated difficulty parameters produce 400 with a field error. Unknown product routes produce 404 Problem Details.

Metadata reads are public. F021 adds protected state/SSE reads using a server-protected identity cookie and roster checks; see [game synchronization](game-synchronization.md) for authorization, reconnects, and guest issuance boundaries. F023 adds [guest bootstrap and private lobby creation](private-lobbies.md). F024 adds [joining and invitations](lobby-joining.md). F025 adds [host settings, protected lobby synchronization, and engine-backed startup](lobby-settings.md). F026 adds [lobby readiness and player management](lobby-players.md). F027 adds [authenticated gameplay actions and the Party lifecycle](party-lifecycle.md). F034 adds the authenticated matchmaking queue; F035 adds game creation and acceptance.

## Party workflow

Party creation uses the host-only lobby start route. F027 adds a server deadline, engine-backed gameplay commands and host-only rematch preparation. The obsolete reserved CreateGameRequest/GameCreatedResponse schemas were removed; there is no second game-creation endpoint. Online matchmaking creates games internally after selecting opponents. Both routes into play use the same GameStateResponse, action contracts, event shape, session store, and shared game engine; there are no duplicated mode-specific gameplay DTOs.

F023 implements guest credential issuance, private lobby creation, and LobbyResponse. F024 implements JoinLobbyRequest and LobbyResponse for joining; F025 adds settings/start; F026 adds member readiness, leave/removal, presence and host migration. The unused speculative JoinGameRequest/JoinGameResponse schemas were removed. The protected cookie now authenticates lobby creation as well as state/events. Identity comes from a verified server-issued credential, not playerId/hostId fields supplied in a gameplay body. Merely possessing a lobby code does not grant identity or host rights.

### Actions and state

Each action includes a caller-generated UUID actionId and string expectedVersion, with a `type` discriminator:

- selectCategory adds categoryId.
- placeBid adds positive wordCount.
- callBluff has no client-chosen bidder, count, or deadline.
- submitWord adds word.
- leaveMatch explicitly leaves the current match; it does not nominate a player ID.
- forfeitMatch explicitly forfeits an active Online match using the authenticated player and the usual action ID/revision guards. Online leaveMatch has the same outcome. Match results include reason (completed, forfeit, or reconnectionExpired) and nullable forfeitedPlayerId; standings and winnerIds remain authoritative even when the winner has fewer points.

No action accepts score changes, validation results, authoritative timestamps, or an acting player ID. The handler derives the actor from identity, compares expectedVersion, serializes operations through the shared session store, and uses Domain guards. Stale state and duplicate action IDs return 409. F014's [application turn layer](turn-management.md) prevents reused IDs from advancing state. F027 implements HTTP execution without automatically retrying commands or adding original-response receipt replay.

GameStateResponse includes session status, revision, server time, public player identity/name/score/presence (including F028 hasLeft and nullable reconnectUntil), a nullable current round, fixed match settings, and a nullable final match result. F020 final results contain standings, shared-winner IDs, tie status, and completed round count; see [match completion](match-completion.md). A round exposes its phase, active player, selected category metadata, highest bid, nullable challenge, and nullable scored result. F019 adds RoundResultResponse with winner, submitted text/status, and per-player award deltas and frozen totals; see [round results](round-results.md). Challenge responses contain role IDs, required count, timestamps, outcome, accepted count, and submitted text/status. They contain no full answer datasets or persistence entities. GameStateMapper projects detached F008 snapshots without mutating them. Category selection includes its server deadline; challenge deadlines and serverTime support client display estimates without deciding outcomes.

### Matchmaking specification

F034 implements POST /api/v1/matchmaking/searches, GET /api/v1/matchmaking/searches/{searchId}, and DELETE on that resource (204 when cancelled). GET /api/v1/matchmaking/status returns the caller's current search or idle. Identity comes from the verified guest session. MatchmakingResponse includes searchId, status, timestamps, a nullable matchId for a selected pair reservation, and a nullable gameId. F035 populates gameId when a pair is selected. POST on searches/{searchId}/accept or /decline accepts an empty JSON object; GET /api/v1/matchmaking/events streams current-state notifications. Ready means both accepted and gameplay is active; failed permits a fresh search. See [matchmaking queue](matchmaking-queue.md) for concurrency, cancellation conflicts, expiry, and ownership checks. These routes and DTOs are included in generated OpenAPI/client types.

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

400 means invalid input; identity/membership checks use 401/403; 404 means absent resource; 409 means state/revision conflict; 500 is an unexpected server failure. Unexpected exceptions are logged server-side and use a generic public title. Stack traces, exception messages, request bodies, and credentials are not included in the response. Error instance excludes the query string.

ApiProblems centralizes current validation/status errors and the exception handler's response shape. The frontend centralizes HTTP failures as ApiError, preserving status, traceId, and field errors. Non-JSON/empty proxy errors use a generic status-based message. Network errors, aborts, and invalid JSON on successful responses remain transport/protocol failures. The client never retries requests automatically.

## OpenAPI and TypeScript generation

Development hosts expose the OpenAPI JSON document at `http://localhost:5000/openapi/v1.json`. It is not exposed by default in Production. The document includes implemented category and protected state/SSE paths, plus registered public DTO schemas. Action variants are represented as unions with literal required type fields; mapped game response metadata supplies native nullable schema references and named components.

With the Development API running, run from `frontend/`:

```sh
npm run api:generate
npm run api:check
```

Pass a different document URL as a trailing argument if needed, for example `npm run api:generate -- http://localhost:5080/openapi/v1.json`. The same applies to api:check. The generator runs openapi-typescript, formats its output using project Prettier configuration, and writes `src/services/api/generated.ts`. It validates references and fails if the document cannot be fetched. The check command fails on stale types; it requires a running current backend and is not a substitute for rebuilding it.

Commit generated changes with the C# contract change. Do not edit generated types manually. Generated code is excluded from ESLint but checked by TypeScript, formatting, and regeneration. LF is fixed for the generated file so checks are portable across Git newline settings.

UUIDs, timestamps, and revisions remain strings in generated TypeScript; generation does not construct Date objects, parse revisions into numbers, or rename JSON fields.

Use `createApiClient`/`apiClient` for JSON transport and feature service wrappers such as `categoriesApi` for typed operations. They use VITE_API_BASE_URL (empty means same-origin), encode path/query values, preserve request options and AbortSignal, and share error handling. Components must not duplicate fetch logic or API type declarations. Request/response wrappers import `components`/`operations` from generated.ts. Success types are compile-time contracts; no runtime response-schema validator is included.

The base client retains standard fetch behavior; credentialed feature wrappers explicitly send credentials. gamesApi retrieves protected state, while guestsApi and lobbiesApi implement account-free creation using the same cookie/CORS policy. No bearer token is stored in JavaScript.

## Validation and remaining scope

Backend tests cover metadata privacy/filtering, Problem Details, route availability, development OpenAPI, schema references/nullability, strict action serialization, and the same state projection for 2/4 players. Frontend tests cover JSON transport, headers, cancellation, encoding, error handling, safe relative paths, and no retries. Live smoke checks exercise the actual frontend client against category success, 400, and 404 responses.

Run backend build/tests/format checks and frontend test/lint/typecheck/build/format checks. Regenerate/check types against the rebuilt Development API whenever contracts change. F021 implements protected state/SSE reads and cookie authentication infrastructure. F023 implements guest issuance and private lobby creation. F024 implements lobby joining with server membership checks. F027 implements game action execution and the private match/rematch lifecycle. Matchmaking and remaining workflow validation are later work.

Implementation references: [ASP.NET Core OpenAPI customization](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0), [openapi-typescript tooling](https://openapi-ts.dev/cli).
