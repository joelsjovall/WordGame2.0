# Development instructions

## Read first

- Read [project context](docs/project-context.md) for scope, roadmap, and handoff status.
- Follow [architecture](docs/architecture.md) for layer responsibilities, gameplay authority, multiplayer, storage, and security decisions.
- Inspect Git status before editing. Preserve unrelated local changes; never discard or overwrite them without explicit authorization.

## Implementation

- Work on one GitHub issue at a time. Keep changes within its requirements.
- Both game modes must use one server-authoritative gameplay engine. Validate gameplay actions and permissions on the backend.
- Keep code modular and dependencies minimal. Avoid oversized components or services, duplicated functionality, and premature infrastructure.
- Separate frontend presentation, API communication, and gameplay logic. Use strict TypeScript and established C# conventions.
- Treat V1 as reference only. Review, refactor, and test any migrated logic; never copy it blindly.
- Remove code only when confirmed unused. Write appropriate automated tests and run relevant builds, linting, and tests when available. Report checks that could not run.
- Never commit secrets or credentials. Never reuse or reproduce the previously exposed V1 database credential; its rotation remains unverified until evidence confirms it.

## GitHub workflow

### Issue requirements and verification

Apply this checklist to every GitHub issue, completing steps 1-4 before implementation:

1. Retrieve and read the complete issue description from the WordGame2.0 GitHub repository when GitHub access is available.
2. Identify all Tasks, Acceptance Criteria, Dependencies, and relevant architectural requirements.
3. Review related project files before making changes.
4. Create a short implementation plan.
5. Implement only the requirements of the active issue.
6. Run relevant tests, linting, and build checks.
7. Compare the final implementation against every Acceptance Criterion.
8. Report which criteria are completed, which remain incomplete, and any known limitations.
9. Never claim an issue is complete if its Acceptance Criteria have not been satisfied.
10. If GitHub access is unavailable, ask the user to provide the issue description rather than guessing its requirements.

### Branch and pull request workflow

1. Inspect the issue requirements and local changes, then start from updated `main`.
2. Create a dedicated feature or documentation branch.
3. Implement the issue, run appropriate checks, and review the diff.
4. Commit and push, create a pull request, then merge and close the issue.

Track work in GitHub Projects: Backlog -> In Progress -> Done. Preserve unrelated work when preparing a branch.

**Current authorization rule:** Ask the user before changing branches, committing, or pushing. Do not merge without user authorization. Preparing code or documentation does not authorize these Git operations.

Keep permanent instructions here, architectural decisions in `docs/architecture.md`, and changing progress or handoff notes in `docs/project-context.md`. Update the relevant document when decisions or status change; avoid duplicating content across them.
