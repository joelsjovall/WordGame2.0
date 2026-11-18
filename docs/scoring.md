# Scoring

F018 uses one backend scoring policy for Party and Online games. The owner delegated the choice of rules on 2026-11-18 after being offered the model below. This finalizes the policy required by [F018](https://github.com/joelsjovall/WordGame2.0/issues/20).

## Rules

- The bidder earns accepted unique word count times the category's points per word, on success or failure.
- Easy defaults to 1 point per word, Medium to 2, Hard to 3.
- Success additionally awards the bidder a fixed 50-point bonus.
- Failure instead awards the caller the fixed 50-point bonus. The bidder keeps partial word points.
- Other players receive nothing. Rejected words, duplicate spellings, and aliases of already accepted answers earn nothing.
- Pending or cancelled challenges award nothing. There are no penalties or negative totals.

For a Medium bid of five words: success awards the bidder 60 points and the caller zero; failure with three accepted words awards the bidder 6 and the caller 50. Failure with no accepted words awards only the caller's bonus.

The [V1 review](v1-migration-review.md#reviewed-gameplay-and-scoring-semantics) recorded the same word-points/bonus recipients, including partial points on failure. V2 explicitly defines difficulty defaults and keeps separate per-player awards rather than V1's misleading combined AwardedPoints value. No V1 code or storage dependency was copied.

## Configuration

`Gameplay:DifficultyMultipliers:Easy`, `Medium`, and `Hard` configure positive integer points per accepted word (defaults 1/2/3). Set all three to 1 to disable difficulty differences. The value is captured with the category at selection, so provider changes cannot alter an active round's word value.

`Gameplay:Scoring:OutcomeBonus` configures a nonnegative fixed bonus (default 50; zero disables it). The bonus is not multiplied by difficulty. Environment variables use double underscores, for example `Gameplay__Scoring__OutcomeBonus=25`. Settings apply across this backend instance; per-lobby configuration remains future work. Configuration is immutable for the host lifetime.

## Authority and atomicity

Domain `ScoringRules.Calculate` accepts only a completed round with a succeeded or failed challenge. Application `ChallengeService` resolves the challenge, completes the round, calculates awards using the retained category multiplier, and calls `Game.RecordRoundScores` in the same isolated session transaction. This applies both to the final valid word and timeout resolution.

The game rejects a second award for a round; phase, action replay, session revision, and store serialization guards prevent concurrent requests from scoring twice. Checked arithmetic protects both award calculations and accumulated totals. Overflow rejects the action as InvalidState and rolls back submissions, outcomes, scores, revisions, and action IDs together. An unrepresentable result needs corrected server settings/state; repeated retries do not silently truncate points.

Shared session state exposes updated totals through `Game.Scores`, and the existing public state mapper projects these into player scores. Clients submit words rather than score values, outcomes, difficulty multipliers, or validation results. Game HTTP actions and authenticated identity are still future adapter work; this issue does not add those endpoints or a playable frontend.

## Verification

Domain tests cover success/failure recipients, difficulty values, configurable/disabled bonuses, invalid and incomplete results, duplicate awards, and overflow. Application tests cover completion, partial failures, aliases, 2–4 players, replay rejection, and transactional overflow rollback. Real-store race tests verify one award during competing timeout and final-word requests. Hosted tests verify the configured scoring policy and public state projection. See [testing](testing.md) for commands.

F018 acceptance review:

| Criterion                         | Evidence                                                                                                           |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| Explicit rules                    | Rules and examples above; owner delegated the final choice.                                                        |
| Consistent finalized calculations | Domain ScoringRules, with checked integer arithmetic.                                                              |
| Correct success/failure changes   | Domain and engine tests verify each recipient, including partial and zero-word failure.                            |
| Correct difficulty modifiers      | Tests cover 1/2/3, custom values, retained snapshots, and uniform word values.                                     |
| Frontend cannot manipulate scores | Backend-only calculation from stored submissions and retained category settings; public projection exposes totals. |
| At most one award                 | Aggregate replay guard plus real-store concurrent completion tests.                                                |
| Independent of game mode          | Shared Domain policy and Application service, exercised with 2–4 players.                                          |
| Automated tests pass              | Backend suite, warning-free Release build, and format verification required before handoff.                        |
