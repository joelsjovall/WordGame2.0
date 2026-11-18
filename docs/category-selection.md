# Category selection and difficulty

F015 connects the shared GameEngine to `ICategoryProvider` through Application's `CategorySelectionService`. Both modes use the same selection, difficulty, snapshot, and timeout rules. No new game HTTP endpoints or frontend workflows are introduced.

## Availability and selection

The engine accepts a category **ID**, never client-authored category metadata or answers. `GetAvailableAsync(difficulty?)` returns provider metadata filtered by enabled difficulties and sorted by ordinal ID. Easy, Medium, and Hard use the existing Domain enum; public JSON uses the existing lower-camel-case representations.

A selectable ID must be listed in the current provider catalog, resolve to a definition with the matching ID and valid accepted answers, and have an enabled difficulty. `CategoryDefinition` already enforces nonempty, unambiguous answers and immutable data. Blank IDs produce `InvalidAction`; unknown, disabled, removed, or unlisted categories produce `CategoryUnavailable`. Provider failures and inconsistent definitions fail explicitly rather than masquerading as an unknown category.

Every enabled category may be reused in later rounds; there is no history-based exclusion or finite-category exhaustion rule. A provider can add categories without engine changes. See [datasets](category-datasets.md) for JSON authoring and [validation](category-validation.md) for provider contracts.

Provider I/O occurs before the atomic session update. Inside that update, the shared command executor rechecks the revision, action ID, membership, eligibility, active player, and selection phase. A late provider response cannot select for a newer turn or round. Only the current selector may act, and selection cannot be changed after bidding begins.

## Retained round data

Selection stores the provider's immutable `CategoryDefinition`, round ID, and difficulty multiplier in `GameSession.SelectedCategory`. Public round metadata remains in `GameRound.Category`. Session copies retain the immutable definition safely; provider refreshes cannot change answers, aliases, difficulty, or the captured multiplier mid-round.

The next round clears the prior selection and starts a new selection window. F017's [word submissions](word-challenges.md) validate against `SelectedCategory.Definition` without fetching a changed provider definition. These internal snapshots contain accepted answers and must never be serialized directly to players. Existing API mappers continue to expose public DTOs only.

## Difficulty modifiers

Each difficulty has configurable positive integer points per word captured at selection. **Defaults are Easy 1, Medium 2, Hard 3**. F018 applies the retained value when the challenge completes; see [scoring](scoring.md) for the finalized formula, bonus, and replay protection. Set all values to 1 to disable difficulty differences.

Rules copy and freeze allowed difficulties and multiplier mappings. Invalid enum values, an empty enabled set, nonpositive multipliers, and incomplete programmatic multiplier maps are rejected. Category or multiplier values cannot be chosen through a player action payload.

## Selection deadline and timeout behavior

The default window is **45 seconds**, starting when the engine starts a round. Deadline and phase state are authoritative server data. Manual selection must commit before the deadline; selection at or after it is rejected. Invalid, stale, duplicate, or unauthorized requests never reset the window. A turn change during category selection retains the original round deadline.

`GameplayTimeoutWorker` checks active snapshots every second by default. At expiry, it resolves the first available category by ordinal ID, then uses an expected-revision transaction to recheck the phase and deadline, reconcile player eligibility, and select for the current selector. Selection and the transition to bidding commit together. A competing selection or timeout cannot overwrite an already committed result.

If no valid category remains, the match cancels safely without awarding points. Provider failures leave the session unchanged, are logged, and retry on later sweeps. Alternatively, configure cancellation at expiry or disable selection timeouts. F014's disconnect/leave policy still applies; an unplayable match may cancel before selection completes.

Deadlines are exact command cutoffs, while automatic progression occurs on a later sweep and may also wait for provider I/O or a revision-conflict retry. This is not a hard real-time scheduler. No provider/network calls occur while holding the session gate. The gameplay worker has independent category/challenge loops; disconnect cleanup remains a separate worker.

## Backend configuration

Non-secret ASP.NET Core settings (environment variables use `__` in place of `:`):

| Setting                                     | Default      | Meaning                                                                                                                                             |
| ------------------------------------------- | ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Gameplay:CategorySelectionTimeoutEnabled`  | `true`       | Set `false` for an unlimited selection window; deadline is null and only the category timeout loop is disabled.                                     |
| `Gameplay:CategorySelectionTimeout`         | `00:00:45`   | Positive selection duration when enabled.                                                                                                           |
| `Gameplay:CategorySelectionSweepInterval`   | `00:00:01`   | Legacy fallback when `Gameplay:TimeoutSweepInterval` is absent; see [challenge timing](word-challenges.md#configuration-and-background-processing). |
| `Gameplay:CategorySelectionTimeoutBehavior` | `AutoSelect` | `AutoSelect` or `CancelMatch`.                                                                                                                      |
| `Gameplay:AllowedDifficulties`              | All three    | Optional array of enabled difficulty names, e.g. `Easy`, `Medium`; an explicit empty/invalid set is rejected.                                       |
| `Gameplay:DifficultyMultipliers:Easy`       | `1`          | Positive integer stored for an Easy selection.                                                                                                      |
| `Gameplay:DifficultyMultipliers:Medium`     | `1`          | Positive integer stored for a Medium selection.                                                                                                     |
| `Gameplay:DifficultyMultipliers:Hard`       | `1`          | Positive integer stored for a Hard selection.                                                                                                       |

For example, `Gameplay__AllowedDifficulties__0=Hard` restricts game selection to Hard. These settings currently apply across the single backend instance; per-lobby match settings remain Party Mode work. The public category metadata API still lists the underlying provider catalog; future game adapters should use the selection service for game availability.

`IGameSessionStore.GetActiveAsync` supplies detached active snapshots for the worker. It is not an atomic snapshot across all games; each game carries its own revision and is revalidated before mutation. A future store must preserve that optimistic update boundary.

## Verification

Tests cover all difficulties and roster sizes, enabled/disabled and unknown IDs, permissions, locked categories, retained answers across provider refresh, reset windows, deadline boundaries, optional timeout behavior, provider-error retry, and configuration validation. Real-store tests verify competing selections/timeouts commit once and provider awaits do not hold session locks. An API integration test verifies automatic selection from configured JSON data with difficulty and multiplier settings. Run suites using [testing](testing.md).
