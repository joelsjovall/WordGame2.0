# Lobby settings and host controls

F025 adds host configuration, synchronized lobby reads, and match startup. Create a lobby from the homepage, or choose **Open lobby settings** after [joining](lobby-joining.md). The host gets editable controls; other members see the authoritative settings. Refreshing a lobby URL reuses the protected guest cookie and reloads its current state.

## Supported settings

| Setting | Default | Validation |
| --- | --- | --- |
| Maximum players | 4 | Integer 2–4; cannot be below the current roster size. New joins respect this limit. |
| Total rounds | Automatic | Null means two rounds per player, resolved from the frozen starting roster. Explicit integer 1–100. |
| Challenge duration | 30 seconds | Integer 5–300 seconds. |
| Allowed difficulties | Easy, medium, hard | Nonempty list of distinct valid difficulties. |

The global category-provider policy still applies: a match cannot enable a category disabled by the server. Startup rejects a selection with no available categories. The shared engine enforces the match's difficulty subset for both player selection and timeout selection, and uses its frozen duration for every challenge. Existing trusted engine callers without per-match overrides retain global defaults; Online matchmaking is unchanged.

## API and synchronization

All routes below require the server-issued identity cookie. Reads and events require roster membership; nonmembers receive 404. Host writes validate the verified actor, JSON/origin policy, settings, lifecycle, and canonical nonnegative Int64 revision string. Clients cannot supply a host or acting player ID.

| Route | Behavior |
| --- | --- |
| GET /api/v1/lobbies/{lobbyId} | Current LobbyResponse; no-store, Vary: Cookie. |
| GET /api/v1/lobbies/{lobbyId}/events | SSE `lobby` events containing current LobbyResponse snapshots and decimal revision IDs. |
| POST /api/v1/lobbies/{lobbyId}/settings | `{ "expectedVersion": "1", "settings": { "maximumPlayers": 4, "roundCount": null, "challengeSeconds": 30, "allowedDifficulties": ["easy", "medium", "hard"] } }`; 200 updated lobby. |
| POST /api/v1/lobbies/{lobbyId}/start | `{ "expectedVersion": "2" }`; 200 lobby with started status and gameId after engine startup. |

Malformed settings/revisions return 400, missing identity 401, nonhost writes or foreign origins 403, missing/expired lobbies 404, and stale revisions or lifecycle/capacity conflicts 409. Successful joins, settings changes, and startup transitions advance the revision. Failed writes preserve the state. A startup failure restores waiting status with a newer revision so clients recover rather than reuse stale commands.

The in-memory store registers subscriptions and the initial snapshot under the same gate as updates. Each subscriber has a bounded latest-state channel; slow clients can skip intermediate revisions without losing the current state. SSE sends a fresh snapshot on reconnect and at 15-second heartbeat intervals, closes by credential/lobby expiry, and releases subscriptions on cancellation. F026 adds independent lobby presence leases; game presence remains separate. The browser refreshes state after connection errors, ignores older revisions, stops on lost authorization/membership, and closes the stream when leaving the page. Draft host edits reset when authoritative settings change, while unrelated presence/ready updates preserve the draft; save edits before starting.

## Startup and frozen match state

The host must have at least two players within the configured capacity. A matching revision reserves `waiting -> starting` under the store gate; new joins and setting changes are blocked during startup. Existing members may safely retry joining without allocating slots. Category validation and shared GameEngine create/initialize/start run outside the lobby gate using the reserved immutable roster/settings. Success publishes `started` with one game ID; host retries return that committed game without creating another. Simultaneous startup requests conflict while the reservation is active.

Once reserved, startup finishes even if the HTTP request disconnects. Cleanup temporarily preserves a starting lobby so it cannot disappear midway through the operation. Engine startup failure abandons any partially created game and restores the waiting lobby. F027 retains active lobbies and opens a new retention window at match completion; it does not mutate the separate game session or its settings. All state remains single-instance/in-memory and is lost on restart.

F026 adds [readiness, leave/kick, host migration and disconnect presence](lobby-players.md), including all-member ready/connected startup checks and compensation for a disconnect during startup. F027 adds [countdown, playable gameplay controls, final results and rematches](party-lifecycle.md).

## Acceptance review

| F025 criterion | Evidence |
| --- | --- |
| Host configures supported settings | Host editor plus Domain/API bounds and name/enum validation tests. |
| Nonhosts cannot modify settings | Cookie-derived host guard; integration/browser tests verify 403 and hidden controls. |
| All connected members see settings | Protected lobby state plus read-only guest view. |
| Changes synchronize correctly | Atomic snapshots, SSE integration tests, two-browser-context desktop/mobile journey, revision recovery tests. |
| Invalid settings rejected | Bounds, empty/duplicate/unknown difficulties, roster-capacity and stale-version tests. |
| Consistent settings after start | Immutable MatchSettings, post-start mutation guards, API checks, engine duration/manual/automatic difficulty tests. |
| Cannot start an invalid match | Host/count/revision/lifecycle/category guards, startup race and rollback tests, disabled single-player start UI. |
| Relevant tests pass | Focused backend, component/client, and desktop/mobile browser checks; see [testing](testing.md). |

All eight criteria are implemented. Full regression checks remain part of PR CI.
