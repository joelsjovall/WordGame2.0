# Player turns

F014 extends the shared Application GameEngine for 1v1 and 2–4-player matches. One immutable roster order drives turns for both modes; there is no mode-specific rules engine or client-controlled starting-player argument.

## Turn and round policy

- `StartAsync(gameId)` selects the first eligible player in roster order.
- Category selection keeps that player active for the opening bid. Each valid bid advances clockwise to the next eligible player, wrapping around the roster.
- Calling a bluff transfers control to the highest bidder for the challenge. Turn-restricted category/bid/bluff actions are unavailable during the challenge and results phases, even to the player whose turn is retained in history.
- `NextRoundAsync(gameId)` rotates clockwise from the previous round's **original starter**, not its last bidder/caller, skipping unavailable players. `GameRound.StartingPlayerId` survives deep copies; the new round's turn sequence starts at 1.
- The engine checks roster membership, eligibility, current turn, and Domain phase guards. Invalid actions cannot advance a turn or consume an action ID.

The stable roster and historical scores remain intact after someone leaves; eligibility changes rather than removing/reordering participants. Starting a round requires at least two eligible players. The player ID is supplied by a future authenticated adapter, not trusted from a client body.

## Repeated and stale commands

Every implemented player command (select category, bid, call bluff, leave) requires `PlayerActionContext(ActionId, ExpectedVersion)`. Action IDs must be nonempty UUIDs and revisions nonnegative Int64 values. Future HTTP adapters must parse the canonical decimal revision string described in [API contracts](api-contracts.md).

The store checks the expected revision under the same per-session gate as the update, before invoking its callback. A mismatch returns `StaleState`; it never mutates the draft or publishes a revision. Presence, maintenance, and gameplay updates all affect the shared revision, so clients must refresh after any conflict.

Accepted action IDs are retained in the session and copied with its isolated drafts. Reusing an accepted ID at a current revision returns `DuplicateAction`, including in another phase/round or with a different payload/player. A stale replay may return `StaleState` first. IDs are scoped to the game; unrelated games do not share a replay cache. Failed actions leave their IDs available for a corrected request.

These guards prevent duplicate progression; they do **not** yet replay a cached original success response. The reserved HTTP idempotency convention still needs response-receipt storage before exposing retry-safe action endpoints. Rejected actions return no state and leave state/revision unchanged. Cancellation and infrastructure failures keep the existing engine semantics.

## Disconnects and leaving

`ConnectAsync` and `DisconnectAsync` are trusted transport operations using server-issued connection IDs. Multiple tabs retain independent connections; a duplicate/stale disconnect cannot remove another tab's connection. A temporary disconnect preserves identity, scores, and order. During `Sessions:DisconnectedGrace` (default two minutes), the player remains eligible to act/reconnect, so an HTTP command can still arrive while its event stream reconnects.

After the grace period, an offline player is skipped when a bid advances or a round chooses its starter. The existing cleanup worker also reconciles turns at `Sessions:CleanupInterval` (default one minute), so progression does not depend on the disconnected player sending another request. Turn reconciliation uses the same policy inside the session transaction and publishes a shared-state revision only when something changes. Connect/disconnect/leave operations reconcile immediately at their transaction timestamp.

At reconciliation:

| Condition                                                                                                                     | Result                                                                                                |
| ----------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| Category selector or current bidding caller becomes unavailable, with at least two eligible players                           | Skip clockwise; increment turn sequence once. Preserve an available player's highest bid.             |
| Fewer than two eligible players remain in a starting/active match                                                             | Abandon the session and cancel the game.                                                              |
| The highest bidder becomes unavailable during bidding, or either challenge participant becomes unavailable during a challenge | Cancel the match rather than substitute a participant or invent an outcome.                           |
| Waiting session has everyone offline beyond grace                                                                             | Existing whole-session abandonment applies. A lone connected player can keep a waiting session alive. |

Expiry is evaluated on actions/reconciliation/sweeps rather than an exact wall-clock timer. A bluff call cannot target an already ineligible bidder. Reconnecting before an abandonment commits restores eligibility in the original order; it does not seize a turn already passed to another player. Terminal abandoned sessions cannot reconnect.

`LeaveAsync` permanently marks a participant as departed, clears their connections, and reconciles without waiting for grace. `PlayerPresence.HasLeft` records this internal session state. A departed player cannot reconnect or act in that match. Explicitly leaving a waiting session with fewer than two remaining participants cancels it too. Transport heartbeat/stale-connection detection remains necessary: an unreported disconnect still appears connected.

This is a shared safety policy, not Online forfeit scoring: abandonment awards no points and declares no winner. F015 adds [category-selection timeouts](category-selection.md). Party/Online-specific forfeit results, challenge submission, bidding timeouts, and broader scoring remain their own issues. The API still exposes no game/action/SSE routes; public player-departure presentation and HTTP error/receipt mapping remain adapter work.

## Tests

Application tests cover stable/cyclic order and rotating starters for 2, 3, and 4 players; phase/turn permissions; stale/duplicate/malformed actions; cross-round replay; leave/reconnect eligibility; multiple tabs; grace boundaries; and cancelled unplayable matches. Existing lifecycle tests now supply versioned action contexts.

Real-store tests cover concurrent competing actions and duplicate requests, conflict rejection before callbacks, replay guards surviving copies, concurrent maintenance sweeps, revision notifications, and cancellation when a 1v1 opponent never returns. See [testing](testing.md) for commands.
