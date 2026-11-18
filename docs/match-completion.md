# Match completion and standings

The owner confirmed these F020 rules on 2026-11-18: configurable Party length (default two rounds per player), fixed six-round Online matches, highest total score wins, and shared wins for tied top scores. Both formats use the same gameplay engine, scoring, and completion logic.

## Formats and settings

- Party uses a positive total round count. Without explicit settings, the default is twice the initial roster size: 4, 6, or 8 rounds for 2, 3, or 4 players. A host workflow can choose another positive count before creating the game, including counts that do not give equal numbers of starting turns.
- Online requires exactly two players and always uses six rounds. There is no client-selectable Online round limit.
- Trusted lobby/matchmaking workflows call `GameEngine.CreateAsync(players, MatchSettings.Party(roundCount))` or `MatchSettings.Online()`. Settings are immutable and retained in every session copy. The existing creation overload defaults to Party settings.
- No score target, sudden death, extra tiebreak rounds, early finish, or separate mode-specific completion engine is introduced. Existing [scoring](scoring.md) and [rotating starters](round-results.md#next-round-policy) apply.

Settings are backend inputs, not new public request fields. Lobby host controls and matchmaking integration remain later issues.

## Automatic completion

After the configured final challenge completes, ChallengeService records its scores and round result, calls Domain Game.Complete, and marks the session Completed with the store's authoritative completion timestamp. Final-word success and deadline failure use this same path. The entire update commits atomically: submissions, outcome, round scores/results, match standings, session status/end time, revision, and accepted action ID.

Game.Complete requires exactly the configured number of completed and scored rounds. It constructs the immutable final result and changes status once. Starting a round beyond the limit, completing early, and completing again are rejected. Checked score overflow rolls back the final attempt without publishing a premature match result. Normal games finish automatically without another command. F022 removes the unused GameEngine.CompleteAsync facade; Domain Game.Complete retains these aggregate guards.

Earlier rounds stay in the results phase until explicit progression, as specified by F019. A final round transitions directly to the completed match, retaining its scored round result. Cancelled/abandoned matches have no normal match result or invented winner; future forfeits/rematches remain separate work.

## Final standings and ties

The final MatchResult contains game ID, format, completed round count, standings, winner IDs, and IsTie. Every participant appears with immutable ID, display name, final score, and rank, including participants who left the roster's active play.

Standings sort by descending total points. Equal scores share competition rank: scores 100, 100, 60, 20 have ranks **1, 1, 3, 4**. Tied rows retain original roster order only for stable display; that order does not choose a winner. All players at rank one share the win. IsTie means more than one top scorer, not merely a lower-place tie. If all totals are equal, every player shares first place. No additional rounds are played.

## State, authority, and retention

`GameEngineResult.MatchResult` and `State.Game.Result` expose the frozen final result on completion and subsequent reads. `GameStateResponse.match` exposes the fixed format/round count, and nullable `result` exposes the public final standings. The current round result remains available separately under `round.result`; historical round queries continue to work. C# projections and generated TypeScript contracts share this structure.

Completed matches reject category selection, bids, bluff calls, words, challenge resolution, round progression, restart, early/manual repeated completion, leave actions, and cancellation. Presence reconnect/disconnect updates remain allowed for result viewing, preserve the result, and do not change EndedAt or extend retention. Future adapters must verify participant identity and membership before returning shared state; this issue does not map gameplay HTTP endpoints or implement identity/UI.

Results live for the retained session's lifetime (completed retention defaults to ten minutes from completion). Cleanup removes session and result together. Restart loses all in-memory sessions/results. Persistent match history/statistics remain future work.

## Acceptance review

| F020 criterion                  | Evidence                                                                                                                           |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| Documented rules                | Confirmed formats, round counts, scoring winner, tie ranks, and examples above.                                                    |
| Automatic completion            | Both final-word success and timeout failure complete in the scoring transaction.                                                   |
| Correct standings               | Domain tests cover descending totals, player names, shared ranks, and frozen read-only results.                                    |
| Consistent ties                 | Top ties, lower-place ties, all-zero ties, and Online/Party engine flows are tested.                                               |
| Reject completed gameplay       | Application tests verify every gameplay/lifecycle command fails without a new revision.                                            |
| Participant result availability | Shared state/API projections include final standings; reads and reconnects preserve results until retention cleanup.               |
| Shared mode logic               | Formats differ only in creation settings/roster constraint; identical ChallengeService completion path.                            |
| Automated tests pass            | Domain, Application, Infrastructure concurrency/retention, API contract, and generated frontend checks; see [testing](testing.md). |
