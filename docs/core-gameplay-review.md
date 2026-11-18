# Core gameplay review

F022 reviews the core implementation at merged revision `2afdae7` before Party and Online workflow development. This pass preserves established bidding, validation, turn, timer, scoring, round-limit, and tie rules. No public HTTP contracts, frontend behavior, production dependencies, or service interfaces are added.

## Review and responsibility boundaries

| Reviewed area                                                                  | Responsibility and decision                                                                                                                                                                                                         |
| ------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Domain Game, GameRound, Challenge, player/bid/turn/submission models and enums | Aggregate ownership and invariants remain in Domain. Application permission/phase guards intentionally complement those invariants so errors are reported before mutation; they are not separate gameplay policies.                 |
| CategoryDefinition, AcceptedAnswer, Category, WordNormalizer                   | Immutable answer indexing owns normalization and alias lookup. Ambiguous spellings and duplicate canonical identities remain guarded. Remove redundant normalization outside that lookup.                                           |
| ScoringRules, MatchSettings, RoundResult, MatchResult                          | Keep pure scoring/ranking and immutable results. Centralize default difficulty values and retain checked totals, shared tie ranks, and fixed Online settings.                                                                       |
| GameEngine, GameCommandExecutor, GamePhaseGuard, TurnPolicy                    | Keep one application facade, transaction/error boundary, and eligibility policy. Remove the obsolete manual-completion facade method; preserve versioned progression and existing immediate backend progression.                    |
| CategorySelectionService/Rules, ChallengeService/Rules, word validation        | Preserve provider I/O outside the session gate, retained answer snapshots, server timestamps, and one final-word/timeout completion path. Share the bid-satisfaction predicate with Domain.                                         |
| GameSession, snapshots, retention, in-memory store, workers, notifications     | Keep session lifecycle/presence separate from Domain and the serialized store. Copies, cancellation, bounded invalidations, expiry, cleanup, and independent category/challenge timer loops retain their existing responsibilities. |
| API composition, public projections, SSE boundary, frontend synchronization    | Transport continues to expose explicit DTOs and authorize reads; no gameplay policy is moved into HTTP, SSE, or the client.                                                                                                         |

The small provider/store/validator interfaces have real implementations and test uses. They remain useful boundaries. The existing aggregates/services do not need another repository, coordinator, policy framework, or dependency. No other confirmed unused gameplay paths were found in the reviewed scope.

## Focused refactors

- ScoringRules.DefaultPointsPerWord explicitly owns Easy/Medium/Hard defaults. Category rules and host configuration reuse it instead of duplicating enum-ordinal arithmetic. Custom values and configuration keys remain unchanged.
- Challenge.HasMetBid defines the accepted-word threshold once. Submission, timeout eligibility, outcome selection, and the Domain consistency check use that predicate without changing deadline rules.
- WordValidationService calls CategoryDefinition.FindAnswer once with raw input. Remove unused NormalizedText fields from SubmittedWord and WordValidationResult; accepted identity still comes from CanonicalAnswerId and display text stays trimmed/preserved. Pure WordNormalizer behavior and all matching/alias tests remain intact.
- Game.RecordRoundScores finds the completed round once and computes each roster member's prospective total from one award lookup. It creates the immutable result before committing scores, preserving overflow/invalid-batch atomicity while removing an intermediate totals dictionary.
- Remove GameEngine.CompleteAsync: source/reference review found only rejection tests calling it. Since F020, every valid final round already completes Domain and session atomically through ChallengeService. Domain Game.Complete remains the guarded aggregate operation; no valid gameplay path is removed.
- GameCommandExecutor translates KeyNotFoundException to NotFound only when the store never invoked the action callback. Unexpected lookup failures inside gameplay now propagate to the established failure boundary instead of incorrectly claiming the game is missing. No new exception hierarchy or abstraction is needed.

Internal callers should stop requesting manual engine completion or reading the removed normalized diagnostic fields. These were not public transport fields. Existing readiness, roster rotation, disconnection grace, deadlines, nonnegative points, and match rules remain documented in their focused [engine](game-engine.md), [validation](category-validation.md), [turn](turn-management.md), [scoring](scoring.md), [round](round-results.md), and [match](match-completion.md) documents.

## Lifecycle and concurrency findings

The store checks expected revisions under its per-session gate and executes against an isolated draft. Scoring computes checked totals before assignment. Challenge completion, award recording, final standings, session completion, and accepted action IDs remain one update. A failed or cancelled callback commits none of them. Final-word/timeout and concurrent progression race tests remain active.

New real-store tests inject an I/O failure, an unexpected missing-key failure, and cancellation during validation of a final word. They verify unchanged version/timestamp, empty submissions/results, zero scores, and a successful retry with the same action ID. Only the successful retry publishes a new revision; an actually missing game still returns NotFound.

New host integration flows use shipped JSON categories, the real session store, and registered services. They complete Party games for 2–4 players and a fixed six-round Online game, mixing Easy/Medium/Hard categories, opening/raised bids, aliases, duplicates, rejected words, successful and timed-out challenges, historical results, cumulative scores, final standings, and authorized participant reads. Workers are held at a long sweep interval only in these controlled-clock tests; dedicated worker integration tests continue to verify automatic sweeps.

## Acceptance review

Local verification passed: 355 backend tests, 26 frontend tests, and all six desktop/mobile Playwright smoke tests. The warning-free Release build, backend format verification, frontend lint/build/format checks, diff check, and 117 local documentation links also passed. Public DTOs are unchanged, so no API type regeneration is required for this refactor.

| F022 criterion                                       | Evidence                                                                                                                                                                                         |
| ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| No confirmed unused/duplicated gameplay code remains | Removed the confirmed unused completion path/diagnostic storage and consolidated threshold/default-point calculations; retained boundary checks and contracts have concrete responsibilities.    |
| Clear service responsibilities                       | Audit above; Domain rules, Application orchestration, Infrastructure serialization, and API/client transport remain separate.                                                                    |
| Gameplay behavior unchanged                          | Existing rules retained; full-match regressions verify exact score/turn/result outcomes for both formats. Unexpected internal lookup failures are correctly distinguished from missing sessions. |
| Existing automated tests pass                        | Backend, frontend, and browser checks are required before handoff; see [testing](testing.md). Tests for removed internal members now verify their surviving behavior/boundaries.                 |
| Important edge cases tested                          | New callback-failure/cancellation rollback tests, explicit difficulty defaults/invalid enum test, plus existing deadline/replay/overflow/concurrency coverage.                                   |
| No unnecessary dependencies/abstractions             | No new production packages, classes, or interfaces; a test clock is reused by two integration suites.                                                                                            |
| Ready for Party/Online development                   | Shared engine and retained session/state boundaries are verified; remaining workflows have explicit integration points.                                                                          |

Core gameplay is a backend foundation, not a playable multiplayer product yet. Guest issuance, lobby/host workflows, gameplay action endpoints/UI, matchmaking, rematches/forfeits, and persistent statistics/history remain subsequent issues. This review does not add those features or claim they are complete.
