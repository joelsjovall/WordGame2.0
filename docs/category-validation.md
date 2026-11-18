# Categories and word validation

F006 defines storage-independent answer models and deterministic validation. Both multiplayer modes use the same `IWordValidationService`. There is no database dependency.

## Data model

- `Category` remains immutable public metadata: string ID, name, and Easy/Medium/Hard difficulty.
- `AcceptedAnswer` contains a canonical display `Value`, read-only display `Aliases`, and a canonical `Id`. An explicit ID is optional; without one, it defaults to the normalized canonical value. IDs are opaque, ordinal, case-sensitive identities and are not normalized when explicitly provided.
- `CategoryDefinition` pairs metadata with at least one answer. It copies input collections and builds a frozen normalized-text lookup once at construction. It is an immutable server-side snapshot, not a public category DTO.
- Blank values/aliases/explicit IDs, null answer entries, duplicate answer IDs, and spellings that resolve to different answers are rejected with argument exceptions. Error messages identify inconsistent data. A redundant alias for the same answer is harmless.
- Answer IDs need only be unique within a category. A challenge uses one category snapshot. Prefer explicit stable answer IDs when canonical wording may change.

The F007 JSON provider can map this shape into the model; this example specifies the design, not an implemented loader:

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

An answer may additionally supply `id`, for example `capital:copenhagen`, so editing its display value need not change identity. JSON serialization, schema enforcement, initial datasets, and file paths are F007 work. Providers must map data into these models rather than adding JSON/HTTP/EF attributes to Domain.

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

The future submission use case must obtain history from the current server-owned `Challenge.Submissions`, use the snapshot for that round's selected category, and validate the acting identity/phase/deadline. Client-supplied lists of existing words or validation statuses are not authoritative.

For nonblank input, that use case can construct a `SubmittedWord` using the result, acting player, and server timestamp, then call `Challenge.RecordSubmission`. Blank requests are rejected before constructing a SubmittedWord, whose text invariant forbids blanks. Challenge's canonical identity guard remains a second defense against double counting. Validation and recording must be serialized together within a game session; two concurrent calls must not both validate against stale accepted history.

`WordValidationService` is stateless and registered as a singleton in the API composition root. There is no validation HTTP endpoint or automatic challenge completion yet; F017 will provide orchestration.

## Retrieval and cache contract

`ICategoryProvider` lives in Application, with future implementations in Infrastructure:

| Operation | Contract |
| --- | --- |
| `GetCategoriesAsync(difficulty?, cancellationToken)` | Read-only metadata catalog, optionally filtered by difficulty; no answers exposed in the catalog. |
| `GetByIdAsync(categoryId, cancellationToken)` | Immutable CategoryDefinition snapshot, or null for an unknown ID. |

Provider implementations must reject blank IDs and invalid difficulty enum values, use trimmed ordinal category IDs, and honor cancellation. Malformed source data and retrieval failures must produce explicit errors, not null/empty success results. Callers must distinguish an unavailable provider or invalid dataset from an unknown category.

The planned F007 JSON provider will load and validate the complete configured dataset once at startup or first use, reject duplicate category IDs, construct immutable snapshots, and retain the catalog and snapshots in memory. Repeated lookups should return cached snapshots without rereading files or rebuilding answer indexes. No file watcher, distributed cache, or database is needed initially.

A future external provider can implement the same contract, mapping remote answers and aliases to stable canonical IDs. Its caching, expiration, retry, and failure rules belong to that provider. Word validation itself remains unchanged.

When gameplay orchestration is implemented, each active round should retain its selected snapshot until completion so provider refreshes do not alter accepted answers mid-challenge. Snapshot replacement affects new selections only. The round/session snapshot retention policy is designed here; it is not a live engine implementation.

## Verification and remaining scope

Domain tests cover immutable answer definitions, defaults/explicit identities, value/alias collisions, duplicate IDs, missing data, shared whitespace rules, and lookup behavior. Application tests cover canonical/alias matching, invalid words, culture independence, history and cross-call duplicate handling, provider substitution, independent challenges, deterministic results, and real validation results driving a challenge. An API integration test verifies service registration without database/provider dependencies.

All six F006 acceptance criteria are satisfied by these models, contracts, validator, and tests. Concrete JSON loading/caching, datasets, provider registration, and file-error tests are F007; external APIs, gameplay endpoints, session serialization, timers, and public DTOs are later work.
