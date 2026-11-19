# Online matchmaking queue

F034 (#36) adds authenticated FIFO opponent selection for 1v1. It creates a short-lived pair reservation, not a game. F035 owns game creation, acceptance/decline, notifications, and entry into gameplay through the shared engine. No Online UI is added in this issue.

## API

All routes require the existing verified guest cookie. Identity and session IDs never come from request parameters. Responses disable caching; search resources are visible only to their owner.

| Method/path | Behavior |
| --- | --- |
| POST `/api/v1/matchmaking/searches` | JSON `{ "displayName": "Alice" }`; returns 200 with the current search, creating one if needed |
| GET `/api/v1/matchmaking/status` | Returns the caller's latest search or `idle` when none exists |
| GET `/api/v1/matchmaking/searches/{searchId}` | Returns the owned search; unknown/other-player searches return 404 |
| DELETE `/api/v1/matchmaking/searches/{searchId}` | Cancels a searching entry (204); terminal cancellations are idempotent; a matched reservation returns 409 |

The resource routes align with F009's versioned search specification. The additional status route represents `idle` without requiring a known search ID. A response contains nullable `searchId`, `createdAt`, `expiresAt`, `matchId`, and `gameId`, plus `status`. `matchId` identifies a selected group; it is never usable as a game ID. `gameId` remains null until F035 integrates game creation. No opponent credentials, private session IDs, or unrelated searches are returned.

Names use the same backend validation as Party lobbies: trimmed, nonblank, at most 32 characters, without control characters. Name collisions between unrelated queue users are not identity collisions. A duplicate join returns the same searching/matched entry without extending its deadline or changing its FIFO position. A different name conflicts until the current search is cancelled or expires. Unknown JSON fields, including actor IDs, are rejected.

POST and DELETE apply the existing same-origin protection. Joining also uses a per-connection-IP fixed-window limit of 30/minute, configurable as `LobbyRateLimits:MatchmakingPermitsPerMinute` alongside existing sensitive-endpoint limits. Changing identities does not reset this budget; 429 responses include Retry-After. See [lobby security](lobby-security.md) for shared-network/proxy limits.

## States and concurrency

`idle` means no retained entry. Joining creates `searching`; FIFO selects the oldest eligible group and transitions its members together to `matched` with one shared reservation ID. A player has at most one searching or matched entry and cannot match itself. The default `FifoDuelPolicy` selects two players. `IMatchmakingPolicy` can select a different compatible group later without changing queue storage or endpoint operations.

`MatchmakingService` owns validation and transitions in Application. Infrastructure's in-memory queue store serializes callbacks with one semaphore, covering eligibility reads, duplicate checks, FIFO selection, cancellation, and reservation updates. Callers receive immutable search records. Active participation is checked against the shared game session store for both modes; a player who has intentionally left a game is no longer a participant for this check.

Cancellation racing with a join has one of two outcomes: cancellation wins and the opponent remains searching, or selection wins and cancellation returns 409. DELETE never silently dissolves an already selected pair. Acceptance/decline behavior belongs to F035; until then an unused reservation expires.

Joining/status/cancellation and the existing session-cleanup worker prune expired, revoked-session, and now-active-game entries. If one member of a reservation becomes ineligible, both sides become expired and can explicitly search again. Queued eligibility is checked at queue-operation time; entering a Party match afterward invalidates the reservation on the next operation/sweep. F035 must recheck eligibility when committing an actual game. This issue does not allocate games or claim an atomic transaction between independent future game-creation workflows.

## Expiration and storage

| Setting | Default |
| --- | --- |
| `Matchmaking:SearchLifetime` | 2 minutes |
| `Matchmaking:ReservationLifetime` | 1 minute |
| `Matchmaking:TerminalRetention` | 10 minutes |

All durations must be positive. Expiry uses server time and is absolute; polling does not renew it. Revoking/resetting a guest session makes its entries ineligible. Searching entries leave the FIFO immediately when pruned. Cancelled/expired records remain briefly for status recovery, then are physically removed. Cleanup uses the existing `Sessions:CleanupInterval` worker (one minute by default); incoming operations also prune before acting. A fresh search has a new ID, so cancelling an older resource cannot affect it.

The queue is process-local, matching the supported single-backend architecture. Restarts lose searches and reservations. Eligibility currently scans detached active-game snapshots; a player index can replace that query if measured scale warrants it. Distributing queue state or persisting it requires a future implementation of the storage boundary.

## Acceptance review and verification

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

All nine queue criteria are implemented. Tests also verify FIFO with delayed policy selection, both game formats' active-player exclusion, display-name/actor validation, ownership privacy, and same-origin cancellation. Actual Online game creation and acceptance remain F035, as specified in that issue.

Verification passed: 454 backend tests, 73 frontend tests, 12 Party joining/lifecycle browser cases, OpenAPI generated-type consistency, frontend build/lint/formatting, backend solution formatting, and Git diff checks.
