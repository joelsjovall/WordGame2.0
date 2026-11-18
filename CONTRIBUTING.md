# Contributing

Read the [project overview](README.md), [architecture](docs/architecture.md), [coding standards](docs/coding-standards.md), and current [project context](docs/project-context.md). Use [development setup](docs/development-setup.md) to run the app and [automated testing](docs/testing.md) for checks. AI assistants must also read [AGENTS.md](AGENTS.md); its authorization rules apply in addition to this workflow.

## Pick and understand one issue

The 57 planned issues are grouped into six milestones, from Foundation through Public Release; see the [roadmap](docs/project-context.md#roadmap). Milestones group related deliverables, while GitHub Projects tracks **Backlog → In Progress → Done**. Work on one issue at a time and confirm dependencies before starting. Update the project status as work starts and finishes when access permits.

Read the complete GitHub issue, including Tasks, Acceptance Criteria, Dependencies, and relevant discussion. Ask the owner for the description if GitHub cannot be accessed; do not infer requirements from an issue title. Review the affected files and architectural constraints, then write a short implementation plan. Keep scope limited to the active issue; record unrelated work as follow-up issues.

## Branch, implement, and review

1. Inspect `git status` and preserve unrelated local changes. Start from updated `main`; use a fast-forward update and investigate divergence rather than resetting work.
2. Create a dedicated branch such as `feature/F013-game-creation`, `fix/Fxxx-short-description`, or `docs/F012-project-documentation`. Use the actual issue ID and a short description.
3. Implement the issue in focused components/services. Add meaningful tests for changed behavior, update relevant documentation, and remove code only when confirmed unused.
4. Run the relevant tests, formatting, linting, and builds. Review the diff for scope, generated files, configuration, and secrets. Use the [CI guide](docs/ci.md) to interpret failures.
5. Compare the final result against **every** acceptance criterion. Report completed and incomplete criteria, checks run, and known limitations. Do not label an issue complete while criteria remain unsatisfied.
6. Commit with a concise description of the change, push the branch, and open a PR targeting `main`. Respect any approval requirements in the active collaboration session.
7. Address review feedback and failing checks. Merge after review and successful required checks, then confirm the issue is closed and move its project card to Done. An assistant must obtain merge authorization under [AGENTS.md](AGENTS.md).

Do not discard another contributor's changes, rewrite shared branch history, or force-push as routine cleanup. Do not bypass CI failures to make a PR mergeable. See [CI](docs/ci.md) for selecting the final status check as a required branch rule.

## Pull request contents

Use a title identifying the issue and concrete result, for example `F013: Create games from validated session settings`. Explain the problem and resulting behavior, the relevant design choices, and what was tested. Include acceptance-criterion status, known limitations, and UI screenshots when they help review. Link the issue with `Closes #<number>` when the PR is intended to satisfy it.

Keep PRs reviewable and focused on one issue. Mark incomplete work as draft and state what remains. Avoid unrelated dependency upgrades, formatting churn, and speculative abstractions. If scope changes, revise the title and description to match the final implementation.

## Configuration, security, and decisions

Use the [configuration guidance](docs/development-setup.md#configuration-and-secrets); no test needs production credentials. Never reuse or reproduce the exposed V1 credential. Rotation remains unverified until evidence confirms it; a clean V2 secret scan is not evidence of rotation.

For a significant technical choice, update [architecture](docs/architecture.md) and the relevant focused document in the same PR. Record the context, decision, reason, alternatives/tradeoffs, and consequences, and distinguish implemented behavior from intended behavior. Create a separate decision document only when the detail warrants it; link it from architecture rather than copying it across files.

Keep permanent assistant instructions in `AGENTS.md`, architecture decisions in `docs/architecture.md` and focused technical documents, and changing issue progress in `docs/project-context.md`. Link to the source of truth instead of duplicating instructions or adding empty documents.
