# Categories and word validation

F006 defines storage-independent answer models and deterministic validation. Both multiplayer modes use the same `IWordValidationService`. There is no database dependency.

## Data model

- `Category` remains immutable public metadata: string ID, name, and Easy/Medium/Hard difficulty.
- `AcceptedAnswer` contains a canonical display `Value`, read-only display `Aliases`, and a canonical `Id`. An explicit ID is optional; without one, it defaults to the normalized canonical value. IDs are opaque, ordinal, case-sensitive identities and are not normalized when explicitly provided.
- `CategoryDefinition` pairs metadata with at least one answer. It copies input collections and builds a frozen normalized-text lookup once at construction. It is an immutable server-side snapshot, not a public category DTO.
- Blank values/aliases/explicit IDs, null answer entries, duplicate answer IDs, and spellings that resolve to different answers are rejected with argument exceptions. Error messages identify inconsistent data. A redundant alias for the same answer is harmless.
- Answer IDs need only be unique within a category. A challenge uses one category snapshot. Prefer explicit stable answer IDs when canonical wording may change.

The F007 JSON provider maps this shape into the model; see [dataset setup](category-datasets.md) and the [schema](schemas/category.schema.json):

```json
{
  "id": "european-capitals",
  "name": "European Capitals",
  "difficulty": "easy",
  "answers": [
    { "value": "Stockholm", "aliases": [] },
    { "value": "Copenhagen", "aliases": ["Köpenhamn", "Kobenhavn"] }
  ]
}
```

An answer may additionally supply `id`, for example `capital:copenhagen`, so editing its display value need not change identity. F007 supplies the strict JSON loader, initial datasets, and file configuration. Providers map data into these models rather than adding JSON/HTTP/EF attributes to Domain.

## Normalization and matching

`WordNormalizer.Normalize` is shared by submitted text, canonical values, and aliases:

1. Treat null as empty.
2. Split on .NET Unicode whitespace, remove empty segments, and join with one ordinary space.
3. Lowercase using invariant culture; match normalized spellings with ordinal case-insensitive comparison (including case variants such as Greek final sigma). Canonical answer IDs remain ordinal and case-sensitive.

Thus `"  New\t York "` matches `"New York"`, and `" KÖPENHAMN "` matches the configured alias `"Köpenhamn"`. Normalization is idempotent and independent of server culture.

This deliberately extends F005's trim/lowercase baseline to handle internal whitespace. It does not remove accents/punctuation, perform fuzzy matching, collapse different Unicode canonical encodings, or infer translations. Alternative spellings must be explicitly present as aliases. Blank input never matches an answer because blank dataset values are invalid.

Different answers may not share any normalized canonical value or alias. Ambiguous data fails when building the snapshot, rather than silently selecting the first answer or treating the player's word as invalid. Cross-category reuse of a spelling is allowed.

## Validation contract

`IWordValidationService.Validate(categorySnapshot, word, previousSubmissions)` lives in Application. It performs no retrieval, clock access, mutation, or hidden per-player caching. Given the same snapshot, word, and accepted history, it returns the same immutable result:

| Status | Meaning | CanonicalAnswerId |
| --- | --- | --- |
| Accepted | Known spelling whose answer ID is not already accepted in this challenge | Matched answer ID |
| Duplicate | Known spelling whose answer ID was already accepted, including through another alias | Matched answer ID |
| Rejected | Blank or unrecognized input | Null |

The result also includes normalized text. Rejected/duplicate history entries do not consume an answer. Null category/history arguments or null history entries are programming errors and fail explicitly.

F017's [submission service](word-challenges.md) obtains history from server-owned `Challenge.Submissions`, uses the retained round snapshot, and checks actor/phase/deadline. Client-supplied lists of existing words or validation statuses are not authoritative.

For nonblank input, that use case can construct a `SubmittedWord` using the result, acting player, and server timestamp, then call `Challenge.RecordSubmission`. Blank requests are rejected before constructing a SubmittedWord, whose text invariant forbids blanks. Challenge's canonical identity guard remains a second defense against double counting. Validation and recording must be serialized together within a game session; two concurrent calls must not both validate against stale accepted history.

`WordValidationService` is stateless and registered as a singleton in the API composition root. There is no validation HTTP endpoint; F017 now provides submission orchestration and automatic challenge completion.

## Retrieval and cache contract

`ICategoryProvider` lives in Application, with its JSON implementation in Infrastructure:

| Operation | Contract |
| --- | --- |
| `GetCategoriesAsync(difficulty?, cancellationToken)` | Read-only metadata catalog, optionally filtered by difficulty; no answers exposed in the catalog. |
| `GetByIdAsync(categoryId, cancellationToken)` | Immutable CategoryDefinition snapshot, or null for an unknown ID. |

Provider implementations must reject blank IDs and invalid difficulty enum values, use trimmed ordinal category IDs, and honor cancellation. Malformed source data and retrieval failures must produce explicit errors, not null/empty success results. Callers must distinguish an unavailable provider or invalid dataset from an unknown category.

The F007 JSON provider loads and validates the complete configured dataset once when constructed; the API eagerly resolves its singleton before accepting traffic. It rejects duplicate category IDs, constructs immutable snapshots, and retains full and filtered catalogs in memory. Repeated lookups return cached snapshots without rereading files or rebuilding answer indexes. File changes require a restart; there is no file watcher, distributed cache, or database.

A future external provider can implement the same contract, mapping remote answers and aliases to stable canonical IDs. Its caching, expiration, retry, and failure rules belong to that provider. Word validation itself remains unchanged.

F015's [category selection](category-selection.md) now retains each active round's definition in `GameSession.SelectedCategory`. Word-submission orchestration uses that immutable snapshot; provider refreshes affect new selections only and cannot alter accepted answers mid-challenge.

## Verification and remaining scope

Domain tests cover immutable answer definitions, defaults/explicit identities, value/alias collisions, duplicate IDs, missing data, shared whitespace rules, and lookup behavior. Application tests cover canonical/alias matching, invalid words, culture independence, history and cross-call duplicate handling, provider substitution, independent challenges, deterministic results, and real validation results driving a challenge. An API integration test verifies service registration without database/provider dependencies.

All six F006 acceptance criteria are satisfied by these models, contracts, validator, and tests. F007 adds JSON loading/caching, datasets, provider registration, and file-error tests. External APIs, gameplay endpoints, session serialization, timers, and public DTOs remain later work.
