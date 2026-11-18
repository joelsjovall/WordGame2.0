# JSON category datasets

F007 implements `ICategoryProvider` as `JsonCategoryProvider` in Infrastructure. It uses built-in System.Text.Json and the immutable F006 models; no database, extra runtime package, or external service is needed. See [validation design](category-validation.md) for normalization and canonical identity rules.

## Location and startup

The default dataset directory is `backend/WordGame.Api/Data/Categories/`. Each top-level lowercase `*.json` file contains exactly one category. Files in subdirectories are not loaded. The [schema](schemas/category.schema.json) lives outside the dataset directory and must not be copied there.

The API registers one provider singleton and resolves it before accepting traffic. Its constructor reads every category file in ordinal filename order, validates the complete set, builds immutable answer indexes, and caches both full and difficulty-filtered metadata catalogs. A missing/empty directory or invalid file prevents startup; no partial catalog is published.

`Categories:Directory` in `appsettings.json` selects the directory. Relative paths resolve against the API content root, not an arbitrary caller's current directory. An absolute path is also supported. Use `Categories__Directory` as an environment override or `--Categories:Directory <path>` on the command line. The directory is trusted server configuration; category IDs are dictionary keys, never filenames constructed from player input.

The project copies category files to build and publish output. When launching a published DLL directly, run from its publish directory (or explicitly set the content root):

```sh
dotnet publish backend/WordGame.Api --configuration Release --output <publish-directory>
cd <publish-directory>
dotnet WordGame.Api.dll
```

## Adding a category

1. Add a UTF-8 file with a descriptive lowercase filename, for example `european-capitals.json`, in the dataset directory.
2. Use the exact case-sensitive field names and difficulty values in this format:

```json
{
  "id": "european-capitals",
  "name": "European Capitals",
  "difficulty": "easy",
  "answers": [
    { "value": "Stockholm", "aliases": [] },
    {
      "id": "capital:copenhagen",
      "value": "Copenhagen",
      "aliases": ["Köpenhamn", "Kobenhavn"]
    }
  ]
}
```

3. Choose a category ID unique across all files. IDs are trimmed and matched ordinally, with case preserved; lowercase kebab-case is recommended. Difficulty must be exactly `easy`, `medium`, or `hard`.
4. Provide at least one answer. Every answer requires a nonblank `value` and an `aliases` array, which may be empty. Optional nonblank `id` supplies a stable answer identity; otherwise normalized canonical text is used. Prefer explicit IDs before renaming existing answers.
5. Ensure each alias is a nonblank string. Canonical values and aliases must not collide with a different answer after normalization. Repeated spellings for the same answer are harmless. Answer IDs must be unique within the category.
6. Run the checks below, start the API, and restart any running instance to load the new snapshot.

The JSON Schema documents the structural format for editors and tooling. Runtime validation enforces that shape directly without a schema-library dependency, plus domain rules the schema cannot express: normalized ambiguity, unique answer identities, and unique category IDs across files. Unknown fields, duplicate JSON properties, nulls where strings/arrays are required, comments, and trailing commas are rejected.

Errors identify the offending file and underlying cause; JSON parse errors retain line/byte information in the inner exception/message. Duplicate category ID errors identify the later conflicting file. Missing directories raise a directory-not-found error. Invalid/unavailable data is never represented as an unknown category.

## Access and caching

- `GetByIdAsync` trims a nonblank ID and returns its cached CategoryDefinition, or null for an unknown ordinal ID.
- `GetCategoriesAsync` returns the cached read-only metadata catalog, sorted by ID, or a cached difficulty-filtered catalog. Undefined difficulty enum values are rejected.
- Cached reads honor cancellation. Calls do not read or watch files, rebuild answer indexes, or mutate snapshots. The immutable provider supports concurrent reads.
- File edits/removals do not change a running instance. Restarting reloads and revalidates the whole dataset. There is no hot reload or partial refresh.
- Future gameplay orchestration must retain the selected category snapshot for a round. Provider access does not implement category selection or gameplay endpoints.

The initial files contain small representative sets for European capitals (Easy), chemical elements (Medium), and Greek letters (Hard), including localized names and symbol aliases. They exercise all difficulties and answer formats; they are not exhaustive dictionaries or final difficulty balancing. Review and expand them before public play.

## Verification

```sh
dotnet build backend/WordGame.sln --configuration Release --warnaserror
dotnet test backend/WordGame.sln --configuration Release --no-build
dotnet format backend/WordGame.sln --verify-no-changes --no-restore
```

Infrastructure tests load real temporary files, check metadata/difficulty/aliases and validation, reject malformed/incomplete/ambiguous data, detect duplicate properties and IDs, verify cancellation/read-only access, and prove cached snapshots remain usable after source files are changed or deleted. API integration tests load the shipped datasets and verify a missing configured directory prevents startup. Published output is also checked for dataset inclusion and database-free health startup.
