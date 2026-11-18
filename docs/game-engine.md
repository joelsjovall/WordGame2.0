# Core game engine and lifecycle

F013 adds `WordGame.Application.Games.GameEngine`, shared by Party and Online entry points. It composes the existing Domain aggregates and `IGameSessionStore`; it does not own a second game dictionary or depend on HTTP, frontend, matchmaking, or database libraries. The API registers it with the existing shared store and `TimeProvider`, but exposes no new routes.

## Lifecycle

The engine derives phases from existing session/game/round states rather than maintaining a second phase enum that could drift:

| Phase                       | Stored state                                             | Transition                                                                                                |
| --------------------------- | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Waiting for initialization  | Session Waiting, Game WaitingToStart, no round           | `CreateAsync` accepts a fixed roster of 2–4 distinct players with zero scores.                            |
| Initialized, ready to start | Session Starting, Game WaitingToStart                    | `InitializeAsync` begins trusted backend initialization.                                                  |
| Category selection          | Session Active, Game InProgress, Round CategorySelection | `StartAsync` creates round 1 with the first eligible roster member and a configured selection deadline.   |
| Bidding                     | Active / InProgress / Bidding                            | `SelectCategoryAsync` records category metadata and locks category selection.                             |
| Bluff challenge             | Active / InProgress / Challenge                          | `CallBluffAsync` challenges the current highest bid and makes its bidder the active player.               |
| Round results               | Active / InProgress / Completed round                    | `ResolveChallengeAsync` derives the outcome from accepted submissions and completes the round atomically. |
| Next round                  | Active / InProgress / CategorySelection                  | `NextRoundAsync` creates the next numbered round after completed results.                                 |
| Game over                   | Session Completed, Game Completed                        | `CompleteAsync` ends an active match with a completed final round.                                        |
| Cancelled                   | Session Abandoned, Game Cancelled                        | `CancelAsync` cancels a nonterminal session and its unfinished round/challenge.                           |

Completed and abandoned sessions cannot restart; rematches create new sessions. Completed rounds retain their last turn as history. `InitializeAsync` is not a per-player readiness system: lobbies and matchmaking must gather/validate their participants before creating the fixed-roster game.

## Actions and authority

`SelectCategoryAsync`, `PlaceBidAsync`, and `CallBluffAsync` require a roster member holding the active turn. They use the existing Domain phase guards. Positive bids must exceed the highest bid and advance once to the next player in stable roster order through `Game.GetNextPlayerId`; selecting a category leaves that player ready to make the opening bid. A bluff call requires an existing bid, and the caller cannot challenge their own bid.

F014's [turn policy](turn-management.md) now selects/rotates starters, skips unavailable players, handles disconnect/leave reconciliation, and requires action IDs with atomic expected-revision checks for player commands. Duplicate progression is prevented; replaying an original success response remains future transport-receipt work. Category/bidding timeout scheduling remains later work.

F015's [selection service](category-selection.md) resolves player-selected IDs through the provider, enforces difficulty availability and deadlines, and pins immutable answer data. Challenge duration remains a **trusted backend input**: match settings must supply it, and `CallBluffAsync` creates the challenge ID/start/deadline on the server inside the transaction. Nonpositive or unrepresentable durations are rejected.

Word submission/validation orchestration remains a separate use case. It must record server-validated words in the authoritative challenge using the existing validator and atomic store boundary. `ResolveChallengeAsync` accepts no client-selected result: it succeeds as soon as the stored accepted count reaches the bid, otherwise rejects premature completion and marks failure at or after the deadline. The domain still guards counts and terminal resolution. Resolution is explicit; no timer or background challenge worker is introduced here.

The backend match/scoring policy chooses `NextRoundAsync` or `CompleteAsync` after results. F013 does not invent a points formula, difficulty modifier, round limit, winner, or forfeit rule. All lifecycle paths can be exercised without those policies.

## Transactions and results

Creation and reads return `GameEngineResult`; successful results contain a detached `SessionSnapshot`. Rejections contain a structured `GameEngineError` for missing games, invalid arguments/state, membership/turn/eligibility failures, duplicate actions, or stale revisions. Rejections contain no state. These are internal application results, not public API responses; snapshots include internal gameplay data and must be mapped through authorized transport DTOs.

Every mutation executes in one store update. A rejected action throws an internal signal inside the callback so the store discards the draft; the engine then returns a structured rejection. A rejected bid, invalid starter, repeated transition, or premature resolution cannot publish a revision or partially change the authoritative state. Session and Domain lifecycle changes commit together. Cancellation propagates as `OperationCanceledException`; unexpected infrastructure failures are not converted into successful/rejected gameplay outcomes.

Both modes use identical methods and state: there is no mode switch in the engine. Multiple engine instances can share the same store. Per-session serialization and detached reads come from the [session-store contract](session-management.md), keeping games isolated even when their rosters contain the same player IDs. Transport handlers must authenticate caller identity, verify membership/host permissions, and use public projections; IDs alone are not credentials.

## Verification

Application unit tests cover the full lifecycle for 2-, 3-, and 4-player rosters, successful and timed-out challenges, invalid transitions/inputs, unauthorized/out-of-turn actions, rollback/revision preservation, isolated games and snapshots, terminal cancellation, and missing/cancelled requests. A minimal store double isolates application behavior.

Infrastructure component tests run the engine against the real in-memory store to verify competing actions commit one turn change and engine instances share state without cross-game mutation. An API integration test verifies registration uses the host's shared store. See [testing](testing.md) for suite commands.
