# Private lobby creation

F023 adds private waiting-lobby creation without account registration. A lobby starts with one securely identified host and advertises the shared gameplay limits of 2–4 players. It is a pregame resource, separate from the fixed-roster GameSession: no fake second player, game, or alternate gameplay engine is created. F024 adds [joining and invitations](lobby-joining.md). F025 adds [host settings/start and a live lobby view](lobby-settings.md). F026 adds [readiness and player management](lobby-players.md).

## Guest identity and creation API

1. `POST /api/v1/guests` with JSON `{}` obtains an account-free identity. A valid existing identity is reused; otherwise the server generates a new player ID and issues the existing protected HttpOnly identity cookie. The response contains playerId, not a credential. Clients cannot nominate an identity or host ID.
2. `POST /api/v1/lobbies` with `{ "requestId": "<uuid>", "displayName": "Alice" }` requires that cookie. The actor comes from the verified NameIdentifier claim. Use a fresh request ID per intended creation and preserve it when explicitly retrying that intent.
3. A new lobby returns **201** with id, lobbyCode, hostPlayerId, waiting status, the host's public player data, minimum/maximum players, createdAt, and expiresAt. A matching replay returns **200** with the same lobby. There is no lobby read/join endpoint or match-start action in this issue.

Display names are trimmed, nonblank, at most 32 characters, and contain no retained control characters. Request IDs must be nonempty UUIDs. Missing/malformed JSON, unknown fields such as hostPlayerId, invalid names, and invalid IDs produce 400 Problem Details; non-JSON bodies produce 415. Anonymous/forged/expired identities cannot create lobbies. A request ID reused by the same host with a different normalized display name returns 409 without changing the original lobby.

One host can deliberately create multiple independent lobbies using different request IDs. Different hosts have independent request-ID namespaces. There is no production database, guest account record, or client-stored bearer token.

## Identity and browser security

Guest issuance uses F021's existing protected-cookie authentication scheme: HttpOnly, SameSite=Lax, one-day non-sliding lifetime, Secure in Production, and local HTTP allowed in Development. Keep frontend/API hosting same-site; Production requires HTTPS. The normal host Data Protection keys protect identities. Test hosts use isolated ephemeral keys.

Both POST endpoints require JSON and reject explicitly foreign Origin headers. Development permits the explicitly configured frontend origins; Production permits the request's own origin. Non-browser API clients may omit Origin but still need a valid cookie for lobby creation. Development CORS allows credentialed requests only from its configured origins. Lobby codes and public player IDs remain resource identifiers, not authentication credentials.

The frontend `guestsApi.ensure` shares in-flight bootstrap requests and rechecks the cookie on later calls rather than caching an identity indefinitely. `lobbiesApi.create` awaits that bootstrap, sends credentials, preserves the caller's request ID, and propagates server validation/conflict errors without automatically retrying writes. Cancelling one caller does not abort another caller's shared bootstrap; a cancelled creation sends no lobby write after bootstrap. No cookie value is exposed to JavaScript.

## Codes, storage, and deduplication

Lobby codes use six uppercase characters from `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`, avoiding I/O/0/1. Infrastructure generates them with cryptographic randomness. Domain LobbyCode normalizes casing/outer whitespace and validates the alphabet. Lobby IDs are server-generated UUIDs.

Application LobbyService creates the verified host Player and calls ILobbySessionStore. InMemoryLobbySessionStore is part of the existing in-memory session infrastructure and uses one short gate for lobby IDs, code reservations, creation receipts, and cleanup. A collision retries allocation; allocation failure/cancellation does not reserve a request or publish a partial lobby. Concurrent matching requests create exactly one lobby and replay the same immutable state. Independent lobbies do not share members or mutate each other.

The lobby starts with a read-only one-host roster and uses Game.MinimumPlayers/MaximumPlayers as its capacity source. F024 joins enforce capacity and replace immutable snapshots. F025/F026 startup creates a fixed-roster game through the shared GameEngine only when enough verified players are connected and ready.

## Expiry and cleanup

`Sessions:LobbyLifetime` defaults to **00:30:00**, must be positive, and is captured as expiresAt when the lobby is created. Environment overrides use `Sessions__LobbyLifetime`. Creation/replays do not extend the expiry. There is no activity-renewal policy yet.

A waiting/results lobby is unavailable at `now >= expiresAt`, including between sweeps. F027 retains active lobbies beyond the waiting deadline and resets the retention window when the match ends; see [Party lifecycle](party-lifecycle.md). Reads remove that expired entry; creation opportunistically removes expired entries; the existing SessionCleanupWorker also cleans lobbies on `Sessions:CleanupInterval` (default one minute). Expiration releases its ID entry, join code, and host/request receipt together.

Idempotency is bounded by the active lobby's lifetime. After expiry/cleanup, an old request ID is no longer reserved and can create a new lobby; clients should start a new creation intent rather than replay an expired room. This keeps receipts bounded without adding persistent storage. Backend restart loses lobbies and receipts, as it already loses game sessions. Codes are unique among active lobbies and can be reused after expiry.

## Acceptance review and verification

Local checks passed: 71 focused backend tests, five new client tests, and two new desktop/mobile browser tests. Release build, backend formatting, frontend lint/build/formatting, API type regeneration/check, diff checks, and 123 local documentation links passed. The full regression suite remains part of pull-request CI.

| F023 criterion                        | Evidence                                                                                                                             |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Create without an account             | Public guest bootstrap plus cookie-authenticated creation; real desktop/mobile browser flow.                                         |
| Unique active join codes              | Atomic reservations, deterministic collision/retry tests, and concurrent creation tests.                                             |
| Required session information returned | Explicit LobbyResponse and generated frontend types; API tests verify IDs, code, host, waiting status, capacity, roster, and expiry. |
| Creator becomes host                  | Host identity is derived from the protected cookie; spoofed body fields/credentials are rejected.                                    |
| No production database                | In-memory lobby/session store and stateless protected guest cookie.                                                                  |
| Independent lobbies                   | Concurrent same/different-host tests verify distinct IDs/codes and preserved state.                                                  |
| Clear invalid-request errors          | 400/401/403/409/415 tests, including malformed bodies in Development and Production.                                                 |
| Relevant automated tests pass         | Focused Domain/store/API/client tests and new desktop/mobile browser tests; run commands in [testing](testing.md).                   |

The shared game engine's established rules and existing game state/SSE endpoints are unchanged. Future Party and Online integration continues through the same engine rather than moving gameplay decisions into the lobby or frontend.
