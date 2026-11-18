# Real-time game state synchronization

F021 implements authenticated state retrieval and SSE invalidations, plus a reusable frontend subscription service and React hook. Both game formats use the existing shared engine and session store; synchronization never validates words, chooses turns, calculates scores, or decides outcomes.

## Endpoints and authorization

| Endpoint                            | Behavior                                                                                             |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `GET /api/v1/games/{gameId}/state`  | Returns the current public GameStateResponse with revision, server UTC time, deadlines, and results. |
| `GET /api/v1/games/{gameId}/events` | Streams GameEventResponse invalidations as named `game` SSE events.                                  |

Both require an ASP.NET Core protected `wordgame.player` cookie containing a server-issued NameIdentifier claim and a matching member of the game's immutable roster. Missing, forged, or expired credentials return 401 without redirects. Unknown games and nonmembers both return 404. Player IDs in query strings, bodies, event IDs, and lobby codes are not credentials. State and event payloads use explicit public projections: accepted-answer datasets and canonical IDs remain private.

Cookie authentication protects these reads. F023 now supplies [guest identity issuance and lobby creation](private-lobbies.md) using this scheme. Gameplay action endpoints, joining/readiness, and gameplay UI remain later work; their workflows must build rosters from verified identities. There is no test login endpoint, client-selected identity header, committed credential, or anonymous private-game access. Integration tests create protected tickets internally with isolated ephemeral protection keys.

Cookies are HttpOnly, SameSite=Lax, non-sliding, and configured for a one-day lifetime when issued using the scheme. Production requires Secure cookies/HTTPS; Development permits local HTTP. Use same-site frontend/API hosting. Development CORS permits credentialed requests only from explicitly configured frontend origins; production has no cross-origin policy by default. Data Protection uses the host's normal key management; key storage/deployment and guest identity lifecycle must be configured as identity features are introduced. Open streams close at ticket expiry.

## Events and authoritative refresh

Application's GameEventService checks membership for reads/subscriptions and adapts IGameSessionStore.WatchAsync. It owns no second game dictionary. Each subscription immediately receives the current revision; every committed mutation publishes an invalidation, including turns, selection, bidding, challenges, timed completion, scores/results, and presence. Rejected actions publish nothing.

Events contain gameId, decimal-string version, public session status, and updated/removed kind. SSE id equals that revision; retry is two seconds. Responses disable caching and request proxy buffering be disabled. The stream flushes each event and sends named `heartbeat` events every fifteen seconds without a state fetch or revision change.

Store notifications coalesce to the latest revision for slow consumers. SSE is not a guaranteed event-history replay or a stream of deltas. Last-Event-ID does not skip initial synchronization: every reconnect receives the current revision and fetches current authorized state. Session removal emits removed and ends the stream; a fresh state request then returns 404. A cleanup race before watch registration also closes/reconciles through state retrieval.

Each active SSE request registers a unique presence connection, released on request cancellation, expiry, or completion of the stream. Tabs can coexist; closing one releases only its connection. Already-departed players and abandoned sessions can still read retained roster-authorized results without reactivating presence. Existing disconnect grace/abandonment rules remain authoritative.

## Frontend lifecycle and recovery

`gamesApi.getState` uses the shared JSON client with credentials, no-store caching, and AbortSignal. `gameSynchronization` shares one EventSource and one current-state request per game across listeners. It checks IDs and canonical decimal revisions with BigInt, ignores stale/duplicate invalidations, coalesces events during an in-flight request, and never applies an older snapshot over a newer revision.

Opening/reopening always retrieves state. Native EventSource reconnects interrupted streams. While disconnected or when EventSource is unavailable, a single fallback loop retrieves state with delays increasing from two seconds to a thirty-second maximum and retries a closed/unavailable stream. Recovery stops that loop. HTTP 401/403/404 or a removed event closes events, cancels retries, and clears private state; identity changes require releasing the old subscription before subscribing again.

Unsubscribing the last listener closes events, clears the retry timer, aborts pending HTTP work, and ignores late callbacks. Cleanup is idempotent. `useGameState(gameId)` uses React's useSyncExternalStore to rerender subscribers and release them on navigation/unmount. The placeholder homepage is unchanged; gameplay screens can consume this hook as they are introduced.

## Timers

Public round state includes categorySelectionDeadline; challenge state includes its deadline. The state handler supplies serverTime from TimeProvider. `gameSynchronization.remaining(gameId, deadline)` computes remaining display time from the server timestamp plus monotonic performance elapsed time since receipt, clamped to zero. It is unaffected by the browser's wall-clock time or clock adjustments. Missing/invalid deadlines return null.

This is a receipt-based display estimate; network latency and rendering can affect what a client displays. Reconnection/refresh reanchors it. Browser countdowns never resolve challenges or authorize late actions; server deadlines and the existing timeout worker decide results. No timer ticks are sent as gameplay mutations and no healthy-state polling loop is introduced.

## Verification and boundaries

Integration tests stream changes to two/four participants through real engine operations, verify authoritative state/deadlines, reconnect across missed revisions, reject unauthorized identities, end at credential expiry, release presence, and observe removal. Existing store tests cover bounded notifications, failed-update suppression, multiple readers, and cleanup. Frontend tests cover shared subscriptions, navigation cleanup, stale/large revisions, burst coalescing, reconnects, offline fallback/backoff, unavailable EventSource, authorization failure, and monotonic countdowns.

| F021 acceptance criterion                   | Evidence                                                                                    |
| ------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Multiple players receive updates            | Real SSE integration tests with two/four authenticated players.                             |
| Actions reflected without manual refresh    | Engine mutations produce invalidations; client refresh and React rerender tests.            |
| Current state after reconnection            | Initial revision on every subscription, state GET, reconnect integration/client tests.      |
| Temporary interruptions recover             | Native reconnection, reopen refresh, bounded fallback with tested backoff.                  |
| No duplicate subscriptions/needless polling | Shared per-game stream, coalesced reads, idempotent cleanup, no healthy polling.            |
| Private events protected                    | Protected cookie plus membership; missing/forged/expired/nonmember and stream-expiry tests. |
| Server deadline timers                      | Public deadlines/serverTime and tests independent of client wall-clock changes.             |
| Integration tests pass                      | Backend integration and frontend/component checks; commands in [testing](testing.md).       |

Sessions remain in memory on one backend instance and disappear on restart/retention cleanup. F023 supplies guest issuance and lobby creation. Persistent history, actions, remaining lobby/matchmaking workflows, and gameplay UI remain separate issues.

Implementation references: [ASP.NET Core SSE responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0#server-sent-events-sse), [cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).
