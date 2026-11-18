# Timed word challenges

F017 adds Application's `ChallengeService`, composed through the shared GameEngine for both modes. It reuses Domain Challenge, retained category definitions, `IWordValidationService`, and the atomic/versioned session store. Scoring and match progression remain separate work; no game HTTP endpoints are introduced.

## Starting and submitting

Calling bluff captures the highest bid's player and required word count. The server generates the challenge ID, start, and deadline using a configurable positive duration, default **30 seconds**. `CallBluffAsync` accepts no duration or client timestamp. The challenge begins Pending with no submissions and assigns control to its bidder.

`SubmitWordAsync(gameId, playerId, word, context)` processes one word. A future authenticated adapter must derive playerId from identity. Shared guards check membership, eligibility, revision, and action replay; challenge guards require a Pending challenge and its bidder holding the turn. Neither the caller nor another participant may submit for that player.

Validation uses `GameSession.SelectedCategory.Definition` and the current authoritative submission history inside the same transaction as recording. There is no provider read during submission. Provider refreshes cannot alter an active round's answers, and clients cannot supply validation statuses, previous words, canonical answer IDs, or accepted counts.

## Word results and history

| Classification | History                                                        | Accepted count     |
| -------------- | -------------------------------------------------------------- | ------------------ |
| Accepted       | Known answer with a new canonical identity                     | Increments by one. |
| Duplicate      | Repeated canonical answer, including case variants and aliases | Unchanged.         |
| Rejected       | Nonblank word absent from the retained category                | Unchanged.         |

A processed attempt returns `GameEngineResult.Succeeded = true` with `SubmissionStatus`: this means the command was processed, not that the word was valid. Duplicate/rejected attempts are retained with server timestamps. Blank/malformed, unauthorized, stale/replayed, out-of-phase, and late requests are command rejections and add no history entry.

Different action IDs can represent repeated attempts at a spelling; these receive Duplicate word classification. Reusing an accepted action ID instead produces a stale/duplicate-command error without another history entry. See [turn management](turn-management.md#repeated-and-stale-commands) for original-response replay limitations.

## Deadline and completion

The window is `[StartedAt, Deadline)`. The server samples time when the update acquires its session gate, before validation. Requests waiting until the deadline are late regardless of browser send time. A request admitted before the cutoff can finish validation slightly later using its recorded server timestamp; a queued timeout cannot overwrite its committed success.

Reaching the required accepted count marks the challenge Succeeded and completes the round in that same update. The timeout worker marks an incomplete challenge Failed at/after the deadline, retaining timely history. Late words are rejected even before a sweep commits the result. An incomplete challenge cannot resolve early; existing [disconnect/leave rules](turn-management.md) may cancel an unplayable match instead.

History, action ID, outcome, and round completion commit atomically. Phase/outcome guards and expected revisions prevent another completion or submission after a terminal result. Competing final-word/timeout requests publish one completion revision. F017 awards no points and does not automatically start another round.

## Structured result

`GameEngineResult.SubmissionStatus` describes the just-processed attempt. `ChallengeResult` is present once the current challenge is terminal, including on engine state reads. It contains round/challenge IDs, bidder/caller IDs, required/accepted counts, outcome, start, and deadline. It is immutable and derived from committed state. Rejected commands return no state or challenge result.

These are internal application objects; use explicit public projections under [API conventions](api-contracts.md). Internal snapshots retain private answer definitions. Previous rounds keep their Domain challenge history after progression.

## Configuration and background processing

| Backend setting                           | Default    | Meaning                                                  |
| ----------------------------------------- | ---------- | -------------------------------------------------------- |
| `Gameplay:ChallengeDuration`              | `00:00:30` | Positive duration captured at a bluff call.              |
| `Gameplay:TimeoutSweepInterval`           | `00:00:01` | Positive interval for gameplay timeout sweeps.           |
| `Gameplay:CategorySelectionSweepInterval` | `00:00:01` | Legacy fallback when the common sweep setting is absent. |

Environment overrides use `__`, e.g. `Gameplay__ChallengeDuration=00:00:20`. Rules currently apply across one backend instance; per-lobby settings remain later work. Nonpositive durations are rejected. The injected store/worker clock supplies server UTC time; tests use controlled clocks.

`GameplayTimeoutWorker` replaces the category-only worker and shares polling code between independent category/challenge loops. Slow category-provider I/O cannot delay challenge expiry. Disabling category timeouts disables only that loop. Detached active snapshots are rechecked under the session gate; stale/finished-state races change nothing, and unexpected failures are logged and retried.

The deadline is an exact submission cutoff; result publication follows a sweep and is not a hard real-time guarantee. Timers/state still disappear on restart. Transport identity, body/rate limits, and public actions/SSE remain adapter work.

## Verification

Tests cover the five-capital example for 2–4 players, aliases/duplicates, invalid words, actor/phase guards, retained provider snapshots, cancellation/replay rejection, configurable time, deadline boundaries, partial failures, and terminal results. Madrid and Rome were added to the shipped European Capitals dataset so the example also passes against the real JSON provider.

Real-store tests cover concurrent submissions, repeated expiry requests, and a pre-deadline final word racing a queued timeout. Hosted tests cover the full example, automatic expiry without submissions, category-timeout disabling, and challenge expiry while another game's category provider is blocked. See [testing](testing.md) for commands.
