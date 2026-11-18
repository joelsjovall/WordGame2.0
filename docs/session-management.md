# Multiplayer session state

F008 provides one server-owned session model and `IGameSessionStore` contract for Party Mode and Online Mode. Domain gameplay rules do not depend on the store, transport, database, or mode. The API registers an in-memory store and a periodic cleanup worker; no session/action/SSE endpoints are introduced here.

## Ownership and access

- A session has a server-generated GUID (also its Game ID), one Domain Game, a fixed 2-4-player roster, presence information, lifecycle status, timestamps, and a monotonically increasing revision.
- Party lobbies can gather players separately before creating the fixed-roster game; matchmaking creates a session once opponents are selected. A single-player lobby is not a single-player gameplay session.
- `CreateAsync` constructs its own Game from immutable Player values. Callers cannot inject a shared mutable Game into multiple sessions.
- `GetAsync` returns null for a missing session, or a detached `SessionSnapshot`. Its Game is a deep copy of rounds, challenges, submission lists, score dictionaries, and replay guards. Mutating a returned copy has no effect on authoritative state.
- `UpdateAsync` accepts synchronous trusted backend work against a private GameSession draft. A per-session semaphore serializes updates, reads, subscriptions, and cleanup for that session. Other sessions can progress independently.
- After the callback, lifecycle consistency and cancellation are checked. The store makes another deep copy before committing, so retaining the callback's draft cannot mutate committed state afterward. Exceptions, invalid lifecycle combinations, and cancellation before commit discard the draft without changing state or publishing a revision.
- A successful update increments the revision even if its operation is idempotent. Revision means successful transaction, not a gameplay action ID. Gameplay request deduplication remains engine/action work.

Update callbacks must be short, synchronous, and free of external side effects. Do provider/network I/O before the transaction and revalidate relevant state inside it. Do not start background mutation of the draft or synchronously reenter the store from its callback. These are trusted backend interfaces, not a mechanism for executing client-supplied delegates.

`Game.Copy` supplies detached state without persistence attributes or storage dependencies. Immutable values can be shared; mutable aggregate containers cannot. This correctness-first approach copies round history, so cost grows with match length. Serialization/storage adapters can later optimize state handling while preserving these isolation guarantees.

## Lifecycle

Session status describes multiplayer lifecycle separately from Domain's gameplay and round phases:

| Transition | Guard |
| --- | --- |
| Waiting -> Starting | Explicit backend initialization begins. |
| Starting -> Active | Domain Game has entered InProgress. |
| Active -> Completed | Domain Game is Completed. |
| Waiting / Starting / Active -> Abandoned | Explicit abandonment, or everyone has been disconnected for the configured grace period. Domain Game is cancelled too. |

Waiting/Starting must accompany GameStatus.WaitingToStart; Active must accompany InProgress; Completed must accompany Completed; Abandoned must accompany Cancelled. Inconsistent combinations cannot commit. The future engine performs gameplay transitions and sets the corresponding session status within the same update. Terminal sessions cannot restart; a rematch creates a new session.

An update for a missing/removed session throws KeyNotFoundException. Removal and updates use the same entry gate, so a queued request cannot commit against a removed session or recreate it.

## Presence and reconnect

Roster identity is stable across connections and is distinct from authentication credentials. Future authenticated transport handlers must verify session membership before invoking store operations; knowing a session/player GUID is not authorization.

`Connect(playerId, connectionId)` associates a server-issued transport connection with an existing roster member. A player may have multiple connections/tabs. Connecting the same ID again is idempotent; a connection ID cannot belong to two players within a session. Presence snapshots expose counts and disconnection timestamps, not the live connection sets.

`Disconnect` removes only the named connection. A stale disconnect from an old tab cannot disconnect a newer connection. A player is offline only after their final connection closes. Reconnect preserves their Player, scores, current round, and game history.

When all players are offline, the session records the time of the last connection closing; newly created sessions initially have everyone offline. Any reconnect clears that whole-session offline timestamp. One connected player prevents automatic whole-session abandonment.

Completed sessions can reconnect during results retention without extending their expiration. Abandoned sessions cannot reconnect. Individual-player disconnect forfeits, grace UI, identity issuance, and transport heartbeat/stale-connection detection belong to later mode/transport issues. Transport handlers must report disconnects reliably; an unreported stale connection remains connected to this foundation.

## Change notifications

`WatchAsync` is a cancellation/disposal-bound asynchronous stream. Registration and its initial current-revision signal happen under the session gate, avoiding a subscribe/update gap. Each committed update publishes `SessionChange(SessionId, Version, Status, Kind)`. Failed updates publish nothing. Removal emits Removed and completes existing streams.

Signals are invalidations, not state deltas or a durable event log. Each subscriber has a bounded one-item channel: a slow subscriber receives the latest available revision and may skip intermediate versions. Publishing does not execute subscriber callbacks or wait for network writes. Multiple subscribers get independent channels.

Future SSE adapters should authenticate membership, enumerate the stream, forward revision signals, and dispose it on request cancellation/disconnect. Clients retrieve a fresh authorized state snapshot and use revisions to avoid applying older state. Reconnection opens a new stream with the current revision; historical replay is not provided. Public response DTOs are defined separately in F009.

## Automatic cleanup and configuration

The API's SessionCleanupWorker sweeps on a PeriodicTimer using the injected TimeProvider. Configuration:

| Setting | Default | Behavior |
| --- | --- | --- |
| Sessions:DisconnectedGrace | 00:02:00 | All-offline nonterminal sessions become Abandoned once grace expires. |
| Sessions:CompletedRetention | 00:10:00 | Completed sessions are removed this long after completion. |
| Sessions:AbandonedRetention | 00:05:00 | Abandoned sessions are removed this long after abandonment. |
| Sessions:CleanupInterval | 00:01:00 | Positive interval between sweeps. |

Retention/grace durations cannot be negative; zero permits immediate eligibility on a sweep. Expiration is evaluated during sweeps rather than at exact wall-clock deadlines. End timestamps remain fixed despite later presence updates. Environment overrides use double underscores, for example `Sessions__CompletedRetention`.

Cleanup checks each entry under its own gate, cancels abandoned games, publishes version changes, and removes terminal entries once. Concurrent sweeps cannot remove/count a session twice. Removal closes observer streams; waiters see a removed entry rather than reviving it. Failed cleanup is logged and retried on a later sweep; shutdown cancels the worker cleanly.

## Limits and persistence boundary

All sessions, presence, subscriptions, and revisions live in one backend process. **A server restart loses every active session.** There is no database, cross-instance coordination, durable event delivery, distributed lock, or restart recovery. Deploy one backend instance until a coordinated store is implemented.

A persistent store can replace `IGameSessionStore` while keeping Domain gameplay rules and the Application session model. It must preserve atomic draft/commit behavior, detached reads, per-session serialization or optimistic version checks, terminal cleanup safety, and post-commit notification semantics. Persistence mapping/restoration and durable notification delivery are not implemented by this issue.

The in-memory store is registered once per backend host. Retention settings are resolved after host configuration is finalized. API routes, lobby membership/readiness, the GameEngine, forfeit policy, and actual SSE transport remain later issues.

## Acceptance evidence

| F008 criterion | Verification |
| --- | --- |
| Multiple simultaneous sessions | Shared interface tested with separate 2-, 3-, and 4-player sessions. |
| Independent state | Detached reads/drafts, independent games, and deep-copy tests. |
| Concurrent requests cannot corrupt state | Parallel presence updates, competing bids, rollback, cancelled waiters, and independent-session progress. |
| Both modes share interfaces | No mode-specific branches; the same roster/session types support both. |
| Observable changes | Initial/current revisions, multiple observers, failed-update suppression, coalescing, cancellation, and removal stream completion. |
| Safe abandoned-session removal | Fake-clock grace/retention tests, concurrent sweeps, and queued-update/removal races. |
| Replaceable storage | Application-owned interface; Domain has no session store or HTTP/database dependencies. |
| Tests pass | Domain/store unit tests plus an API integration test that observes the hosted worker automatically removing an expired session. |
