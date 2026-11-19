# Lobby synchronization verification

F030 (#32) verifies the Party synchronization delivered incrementally in F025-F029. No additional transport, polling service, or production behavior is needed.

## Existing flow

Lobby mutations publish through `InMemoryLobbySessionStore.WatchAsync`. Each subscriber has a bounded, latest-state channel; intermediate revisions may be coalesced. The authenticated `/api/v1/lobbies/{id}/events` endpoint uses ASP.NET Core SSE, as established for gameplay in F021. `LobbyEventStream` sends full authoritative `lobby` snapshots, revision IDs, and periodic snapshots for connection health. It checks current membership before sending and terminates access with `removed` or `expired` notifications.

Snapshots include the roster, readiness, host, settings, `canStart`, status, countdown, and game ID. These fields carry the required notifications without separate event types for each mutation. Reconnecting subscribers receive current state rather than replaying every historical event. This is a single-instance in-memory state stream, not a durable activity log.

The frontend's `connectLobby` service bootstraps identity and reads state, opens one credentialed EventSource, and refreshes after stream opening or connection errors. Full snapshots update the screen directly, avoiding a GET for each event. `useLobby` rejects older revisions. Recovery reuses F021's shared backoff scheduler, stops when live, and releases streams/listeners/timers on unmount. A native EventSource that is reconnecting is retained; only closed sources are replaced. Membership loss is terminal and never triggers automatic joining.

## Acceptance review

| Criterion | Verification |
| --- | --- |
| Join and leave update connected clients | Both host and guest streams observe roster changes |
| Readiness updates without refresh | Both streams receive host readiness and final `canStart` eligibility |
| Settings stay synchronized | Both streams receive capacity, round count, challenge time, and difficulty |
| Host changes appear correctly | Remaining guest receives host migration after the host leaves |
| Every participant receives startup | Both remaining participants receive `started` with the same game ID |
| Reconnection gets current authoritative state | Reopened stream sees settings changed while disconnected; frontend recovery reads latest state |
| Duplicate subscriptions prevented | StrictMode, rerenders, repeated online events, native reconnect, and unmount cleanup tests |
| Existing SSE infrastructure reused | Existing ASP.NET SSE endpoints, store watchers, and shared recovery scheduler retained |
| Relevant integration tests pass | 24 lobby API tests, including two new multi-client stream scenarios; 28 frontend lobby tests |

Removal notifications and remaining-client membership are also covered. The new integration tests consume actual HTTP SSE responses, rather than inferring delivery from state reads. Assertions use bounded waits and accept coalesced snapshots. Existing reconnection tests cover obsolete callbacks, malformed events, lost membership, and cancelled recovery.

All nine acceptance criteria are satisfied. Frontend lint, formatting, TypeScript/production build, and C# formatting were checked. No production files or API contracts changed, so existing browser journeys were not rerun for this verification-only issue.
