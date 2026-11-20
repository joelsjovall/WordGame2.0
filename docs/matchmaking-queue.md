# Online matchmaking queue

F034 adds FIFO selection; F035 creates one shared-engine Online game per pair and starts it after both players accept. The Online UI remains later work.

## API

All routes require the existing verified guest cookie. Identity and session IDs never come from request parameters. Responses disable caching; search resources are visible only to their owner.

| Method/path | Behavior |
| --- | --- |
| POST `/api/v1/matchmaking/searches` | JSON `{ "displayName": "Alice" }`; returns 200 with the current search, creating one if needed |
| GET `/api/v1/matchmaking/status` | Returns the caller's latest search or `idle` when none exists |
| GET `/api/v1/matchmaking/searches/{searchId}` | Returns the owned search; unknown/other-player searches return 404 |
| DELETE `/api/v1/matchmaking/searches/{searchId}` | Cancels a searching entry (204); terminal cancellations are idempotent; a matched reservation returns 409 |
| GET `/api/v1/matchmaking/events` | Credentialed SSE snapshots named `matchmaking`, including initial state and heartbeats |
| POST `/api/v1/matchmaking/searches/{searchId}/accept` | JSON `{}`; idempotently accepts, starts once both accept |
| POST `/api/v1/matchmaking/searches/{searchId}/decline` | JSON `{}`; cancels both pending assignments and abandons the game |

Responses include IDs, timestamps, status, and the caller's `hasAccepted`. `matched` includes a shared game ID and acceptance deadline; `ready` means active gameplay and has no deadline. `failed` permits a fresh search. Opponent credentials and search resources remain private.

Names use the same backend validation as Party lobbies: trimmed, nonblank, at most 32 characters, without control characters. Name collisions between unrelated queue users are not identity collisions. A duplicate join returns the same searching/matched entry without extending its deadline or changing its FIFO position. A different name conflicts until the current search is cancelled or expires. Unknown JSON fields, including actor IDs, are rejected.

POST and DELETE apply the existing same-origin protection. Joining also uses a per-connection-IP fixed-window limit of 30/minute, configurable as `LobbyRateLimits:MatchmakingPermitsPerMinute` alongside existing sensitive-endpoint limits. Changing identities does not reset this budget; 429 responses include Retry-After. See [lobby security](lobby-security.md) for shared-network/proxy limits.

## States and concurrency

`idle` means no retained entry. Joining creates `searching`; FIFO selects the oldest eligible group and transitions its members together to `matched` with one shared reservation ID. A player has at most one searching or matched entry and cannot match itself. The default `FifoDuelPolicy` selects two players. `IMatchmakingPolicy` can select a different compatible group later without changing queue storage or endpoint operations.

`MatchmakingService` owns validation and transitions in Application. Infrastructure's in-memory queue store serializes callbacks with one semaphore, covering eligibility reads, duplicate checks, FIFO selection, cancellation, and reservation updates. Callers receive immutable search records. Active participation is checked against the shared game session store for both modes; a player who has intentionally left a game is no longer a participant for this check.

Cancellation racing with selection either wins or returns 409. Use decline for pending matches; decline/timeout invalidates both assignments without silent requeueing. Both players receive match-found and ready SSE updates. Reconnection retrieves current state; guest reset closes subscriptions.

Live Party and Online creation use `GameEngine.CreateExclusiveAsync`, serializing roster checks against waiting, starting, and active games. General engine setup retains independent-session semantics. Online games use fixed six-round settings and cannot play until accepted by both players. Creation/start failures abandon partial games and mark both assignments failed. Accepted games use existing disconnect rules.

## Expiration and storage

| Setting | Default |
| --- | --- |
| `Matchmaking:SearchLifetime` | 2 minutes |
| `Matchmaking:AcceptanceTimeout` | 30 seconds |
| `Matchmaking:CleanupInterval` | 1 second |
| `Matchmaking:TerminalRetention` | 10 minutes |

Durations must be positive; polling does not renew deadlines. The matchmaking worker reconciles expiry without client polling. Terminal records are retained briefly, then removed; new searches have new IDs. The earlier reservation-lifetime option remains compatible, but game acceptance uses `AcceptanceTimeout`.

The queue is process-local, matching the supported single-backend architecture. Restarts lose searches and reservations. Eligibility currently scans detached active-game snapshots; a player index can replace that query if measured scale warrants it. Distributing queue state or persisting it requires a future implementation of the storage boundary.

## F034 baseline verification

| Criterion | Evidence |
| --- | --- |
| Join queue | Authenticated POST creates a searching resource |
| Cancel queue | DELETE transitions searching to cancelled; repeated terminal cancellation is safe |
| Retrieve status | Owned-resource reads and current-status/idle reads |
| Automatically match eligible players | Default FIFO policy publishes the same reservation ID to two distinct searches |
| Prevent duplicate entries | Concurrent repeated joins return the same search ID; no FIFO reset |
| No multiple active match assignments | Existing active participants are denied; each player has only one queue reservation; F034 creates no second game |
| Consistent concurrent requests | 20 concurrent players with duplicate joins produce 20 searches and 10 disjoint pairs; cancellation race covered |
| Remove expired entries | Search/reservation expiry, revoked sessions, FIFO removal, terminal retention cleanup |
| Automated coverage | Eight matchmaking HTTP cases plus rate-limit security coverage and existing backend regressions |

The original F034 queue review above predates game creation. F035 additionally verifies acceptance/SSE delivery, timeout/decline races, initialization/start rollback, and exclusive cross-mode game creation.

Verification passed: 454 backend tests, 73 frontend tests, 12 Party joining/lifecycle browser cases, OpenAPI generated-type consistency, frontend build/lint/formatting, backend solution formatting, and Git diff checks.
