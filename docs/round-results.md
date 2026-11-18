# Round results and progression

F019 completes the shared backend flow from a finished challenge through results into another round. It reuses F017 completion, F018 scoring, and F014 turn selection rather than introducing separate gameplay paths for Party and Online modes.

## Completion and result

The final required accepted word succeeds immediately; an incomplete challenge fails at its deadline. The bidder wins on success and the caller wins on failure. Pending or cancelled challenges produce no scored round result. Existing [word challenge](word-challenges.md) cutoff and [scoring](scoring.md) rules apply.

`Game.RecordRoundScores` now freezes a `RoundResult` in the same transaction as the score update and challenge/round completion. Results contain round ID/number, category metadata, challenge ID, bidder/caller/winner IDs, required and accepted counts, outcome, submitted attempts with validation statuses, and every roster member's points awarded and total at that completion. Unaffected players have a zero delta. Historic totals remain the totals at that round, even after later awards.

The immutable result ledger also supplies the existing once-per-round award guard; there is no separate duplicate completion flag. Collections are read-only copies, submitted words and score entries are immutable, and Game copies retain results safely. Invalid awards or checked arithmetic overflow publish neither scores nor a result.

## Reading results

`GameEngineResult.RoundResult` is present for the current scored round on completion and subsequent state reads. `GetRoundResultAsync(gameId, roundId)` retrieves an earlier result after progression or match completion. It returns NotFound for an unknown game/round or a round without a scored result, without changing state or revision. Its State is the latest detached session snapshot; its RoundResult is the requested historic result. Existing ChallengeResult remains a current-challenge summary.

The public `GameRoundResponse.result` contains the current result or null. `GameStateMapper.MapResult` projects the same shape for a historic result. Only submitted text/status and public metadata are exposed; canonical answer IDs, normalized text, accepted-answer datasets, and aliases remain private. TypeScript contracts are generated from OpenAPI.

These are application queries and transport contracts, not new HTTP endpoints. Future adapters must authenticate and verify membership before returning them. Sessions/results remain in memory and disappear on restart or retention cleanup; persistent match history is separate work.

## Next round policy

- Results remain visible until a trusted backend workflow explicitly invokes NextRoundAsync or ends the match. There is no automatic results timer or per-player ready-up requirement between rounds. Pregame lobby readiness remains separate.
- The starter rotates from the previous round's **original starting player**, following stable roster order and skipping unavailable/departed players under F014 rules. It does not depend on winner, bidder, or the final bidding turn. Two eligible participants are required.
- `NextRoundAsync(gameId, completedRoundId, expectedVersion)` verifies the result is the current completed/scored round and checks the session revision under the store gate. Use this overload for delayed workflows. An old round ID cannot advance a later completed round, even with a refreshed revision. Competing requests publish one new round.
- The existing gameId-only overload is a trusted immediate backend operation against the current round. It shares the same scored-result/phase and player eligibility guards; it carries no stale-result intent and must not be used to execute delayed client requests.
- Progression creates a fresh ID and sequential number, clears category metadata, retained answer selection, bids, challenge, submissions, and current result, and begins a new category-selection deadline. The turn sequence resets to one.
- The match ID, ordered player identities/names, presence/connections, departure state, scores, action replay guards, and completed round/result history survive. Prior action IDs/versions cannot be used to repeat gameplay in a new round.
- Incomplete, cancelled, terminal-match, wrong-round, stale, or repeated transitions are rejected. Expired challenges reject new words before the timeout sweep, and completed rounds reject category/bid/bluff/word actions.

Win conditions, match limits, results UI, host controls, and public actions/SSE endpoints remain their own issues.

## Acceptance review and tests

| F019 acceptance criterion             | Implementation and verification                                                                                                       |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Correct completed results             | Winner, answer statuses, score deltas, and frozen totals; success/failure multi-round tests.                                          |
| Consistent retrieval                  | Immutable current/historic reads, shared-engine store tests, public projection privacy tests.                                         |
| Scores before progression             | Completion transaction freezes awards/results; next round requires a scored result.                                                   |
| Round state reset                     | Multi-round tests verify fresh IDs/numbers, empty category/bid/challenge, turn sequence, selection deadline, and null current result. |
| Match state preserved                 | Tests verify roster, presence, ID/creation time, cumulative scores, and retained history; existing replay/leave tests remain active.  |
| Correct next starter                  | Existing rotating-starter/eligibility tests plus 2–4-player multi-round flows.                                                        |
| Invalid/repeated transitions rejected | Phase, round ID, revision, terminal-state, replay, and real-store concurrent progression tests.                                       |
| Multiple rounds per match             | Two full roster rotations with alternating success/failure in one session.                                                            |
| Automated tests pass                  | Domain, Application, Infrastructure, API, and generated frontend contract checks; run commands in [testing](testing.md).              |
