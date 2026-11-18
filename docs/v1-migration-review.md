# V1 gameplay review and incremental migration

Reviewed for F005 on 2026-11-18 against V1 commit [dd9c2399a3f852cd7a49fce309286bea9e78e3f0](https://github.com/joelsjovall/wordgame/tree/dd9c2399a3f852cd7a49fce309286bea9e78e3f0). Findings describe that revision, not future V1 changes. V2 architecture and the F004 domain models take precedence over incidental V1 behavior.

This document records the F005 migration scope. F006 subsequently extends whitespace normalization and implements provider-independent validation with aliases; see [category validation](category-validation.md) for current behavior.

## Component decisions

| V1 source | Review finding | V2 decision |
| --- | --- | --- |
| [WordValidationService](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Services/WordValidationService.cs) | Normalization is pure; validation fetches EF-backed category words, chooses the first duplicate normalized entry, and tracks valid duplicates by normalized spelling. | Reuse normalization now as `WordNormalizer`. Rewrite validation around category/provider contracts in F006/F007, with canonical answer identities for aliases. Do not copy the repository dependency or legacy result DTOs. |
| [RoundService](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Services/RoundService.cs) | Mixes repository load/save with positive/increasing-bid guards and cyclic player selection. Membership/turn checks can be skipped when the EF navigation roster is absent. | Retain positive/increasing bids and strict roster/turn guards already present in `GameRound`; do not create a duplicate bidding service. Refactor cyclic selection into `Game.GetNextPlayerId`. Application orchestration belongs to F013/F014/F016. |
| [GameFlowService](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Services/GameFlowService.cs) | Combines EF queries/writes, locks, snapshots, readiness, timeouts, round creation, scoring, and draft cleanup. State retrieval can mutate game state by processing timeouts. | Rewrite orchestration in Application using Domain models and separate provider/session abstractions. Do not port this service or its EF entities. Preserve reusable rule intent through regression tests. |
| [GameTurnStateService](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Services/GameTurnStateService.cs) | Stores mutable category-selection state separately from persisted rounds, reads the system clock directly, and embeds a 45-second duration. | Do not copy the dictionary or timer policy. F014/F015 should use the shared game state, an explicit clock, and documented configurable rules. |
| [GameRounds endpoints](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Endpoints/GameRounds.cs) | HTTP handlers implement challenge creation, submission validation/persistence, resolution, and result projection. Batch submission and incremental validation use different resolution paths. | Rewrite HTTP adapters over shared use cases in later gameplay/API issues. Keep authoritative stored submissions and bidder/highest-bid checks; do not let clients choose validation results or authoritative challenge settings. |
| [RoundsController](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Controllers/RoundsController.cs) | Adds HTTP validation and exception translation around the EF-bound bidding service. | Do not copy controller/service layering wholesale. Use the existing V2 error boundary and future authenticated action endpoints. |
| [Games endpoints](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Endpoints/Games.cs) | Round start depends on EF, ready state, and a legacy direct-start compatibility branch; opening-bid defaults differ by path. | Keep readiness in multiplayer orchestration; define one deliberate start policy in F013-F016. Do not migrate legacy defaults or compatibility branches as V2 rules. |
| [RoundLiveDraftService](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Services/RoundLiveDraftService.cs) | Stores a presentation draft separately from accepted answers. Drafts are not authoritative validation results. | Defer any live-draft feature until required. Do not copy an unused state store. |
| [CategoryWordRepository](https://github.com/joelsjovall/wordgame/blob/dd9c2399a3f852cd7a49fce309286bea9e78e3f0/Server/Data/Repositories/CategoryWordRepository.cs) and legacy entities | Validation lookup depends on AppDbContext/EF; a WordAlias entity exists, but the reviewed lookup and validator do not resolve aliases. | No EF/MySQL references, database schemas, or entities migrate. F006/F007 will supply local-data provider contracts and alias handling. |

## Duplication and obsolete behavior

- `GetNextPlayerId` is duplicated in RoundService, GameFlowService, and GameRounds endpoints. The private endpoint copy has no callers within that file. V2 has one roster lookup on `Game`; no legacy helper copies are added to services or endpoints.
- GameFlowService counts distinct valid persisted words for timeout resolution; the endpoints also count validation results for incremental/batch completion. V2's `Challenge.AcceptedWordCount` and canonical identity guard are the single domain counting basis.
- V1 validation suppresses repeated valid spellings but does not deduplicate aliases by canonical answer. F004's accepted canonical IDs already provide the guard; F006 must map spellings to those IDs. Choosing the first colliding category entry silently should not become a V2 data policy.
- V1 has a single-player branch that creates a self-challenge. It is incompatible with V2's 2-4-player scope and no-self-challenge invariant, and is intentionally not migrated.
- The legacy direct-start readiness bypass and hardcoded opening bids, durations, bonus values, and random-category timeout behavior are not finalized V2 settings.
- `ResolveChallengeAsync` directly changes scores and has no resolved-state early guard inside the method. A caller should not be able to replay a resolution to award points again. V2 already guards challenge resolution and round awards; future orchestration must preserve those guards under concurrent requests.

## Migrated in F005

1. **Word normalization:** `WordNormalizer.Normalize(string?)` retains V1's null-to-empty, trim, and invariant-lowercase behavior. `SubmittedWord.NormalizedText` uses it while preserving display text and the independently supplied canonical answer ID. It does not collapse internal whitespace, remove punctuation/diacritics, resolve aliases, or declare an answer valid. This is the reviewed baseline for F006, not a completed validation service.
2. **Cyclic roster lookup:** `Game.GetNextPlayerId(Guid)` returns the next participant in the game's stable roster order, wrapping at the end. V1 integer IDs and EF navigation objects become V2 GUIDs and immutable participants. Unknown players are rejected rather than silently selecting the first player; empty/single-player rosters are already excluded by V2 construction.

Neither operation reads a clock, stores session state, performs networking, changes turns automatically, or depends on MySQL. No new packages or application services are needed. The lookup can be used by the future engine for bidding turns or next-round starters without deciding those policies now.

## Reviewed gameplay and scoring semantics

Retained compatibility expectations are: bids are positive and strictly increase; only the active roster member may bid or call bluff; the highest bidder must prove the challenged bid; accepted unique answers count toward the required total; insufficient accepted words fail a resolved challenge; a completed round can be followed by a fresh category-selection round.

V1 chooses the participant after the challenged bidder as the next-round starter. Tests show this sequence can be expressed with the new lookup and models. This records compatibility capability; the automatic starting-player policy still belongs to F014/F019.

The reviewed V1 scoring implementation awards the challenged player accepted-word count times category points, even on failure. It then gives a fixed 50-point bonus to the challenged player on success or the caller on failure. Its returned `AwardedPoints` sums word points and bonus even when they go to different participants, so that field is not a per-player score delta.

**That formula is a V1 finding, not an approved V2 rule.** F018 explicitly requires finalized scoring rules before implementation. No 50-point bonus, difficulty multiplier, timeout duration, or score formula is introduced by this migration. Existing `PlayerScore` and `Game.RecordRoundScores` remain generic server-supplied award representations.

## Verification and boundaries

- Adapted V1's three normalization examples (Volvo/BMW/Audi), plus null/blank/Unicode whitespace and invariant-culture checks.
- Tested roster order and wraparound for 2, 3, and 4 participants; rejected unknown identities, deliberately unsorted GUIDs/names, side-effect-free lookup, and integration with `GameRound.AssignTurn`.
- Exercised success/failure bid -> raise -> bluff -> submissions -> resolved round -> next round flows with both 2-player and 4-player games. Duplicate/rejected words do not increase the accepted count; repeated resolution and bidding during a challenge are rejected.
- Existing domain tests cover additional lifecycle, ownership, cancellation, bid, and atomic score invariants.
- Tests use trusted synthetic validation results. They do not claim that a category provider or complete word validator exists. V1's database-dependent server/test suite was not executed; source behavior was reviewed and selected expectations re-expressed as V2 tests.

F005 is intentionally an incremental migration. Full validation remains F006/F007; engine/turn/timer/scoring/progression services remain F013-F019. The live multiplayer game is not implemented yet. Within this selected scope, all six F005 acceptance criteria are covered by the review, minimal migration, architectural boundaries, absence of old configuration/services, regression flows, and automated tests.

## Security follow-up

No V1 configuration, connection strings, credentials, deployment files, or database contents were copied or used. The previously exposed database credential must still have its rotation verified through owner/provider evidence. Reading public source or removing a credential from a file cannot verify revocation; rotation remains unverified.
