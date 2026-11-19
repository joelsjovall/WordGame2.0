# Development Instructions

## 1. Core Principles

- Implement only what the active GitHub issue requires.
- Prioritize correctness, security, maintainability, and token efficiency.
- Keep changes focused. Avoid unnecessary refactoring, abstractions, dependencies, and documentation.
- Prefer minimal diffs over rewriting entire files.
- Keep responses concise. Avoid repetition, excessive explanations, and unnecessary progress updates.
- Never sacrifice correctness or required verification to save tokens.

## 2. Project Context

- Repository: `joelsjovall/WordGame2.0`.
- V1 reference: `joelsjovall/wordgame` (reference only).
- Consult `docs/project-context.md` for scope, roadmap, and handoff information.
- Follow `docs/architecture.md` for architectural decisions, responsibilities, multiplayer, persistence, and security.
- Read only relevant documentation sections when sufficient context is already available.
- Do not repeatedly read unchanged files or documentation.
- Keep permanent instructions in `AGENTS.md`, architecture decisions in `docs/architecture.md`, and changing progress in `docs/project-context.md`.

## 3. Token Efficiency

### Context Management

- Read only files relevant to the active task.
- Use targeted searches (`rg`, `rg --files`) before opening files.
- Prefer specific file sections over entire files.
- Do not scan the entire repository unless necessary.
- Avoid printing large directory trees, file contents, terminal logs, or command outputs.
- Reuse information already established in the current session.
- Do not repeatedly investigate resolved questions.
- Avoid exploring unrelated features or speculative improvements.

### Planning and Execution

- For simple, localized changes, proceed without a formal plan.
- For nontrivial tasks, create a brief plan of 2–4 steps.
- Do not repeatedly restate the plan or task requirements.
- Implement the smallest complete solution that meets the acceptance criteria.
- Avoid unnecessary back-and-forth when requirements are clear.
- Ask questions only when missing information materially affects correctness or safety.
- Batch related inspection commands when practical.
- Avoid repeated failing commands without diagnosing the cause.

### Communication

- Do not narrate routine tool calls.
- Avoid lengthy explanations unless explicitly requested.
- Report meaningful findings, blockers, and verification results.
- Keep final responses short and structured:
  1. Changes made.
  2. Tests and verification.
  3. Remaining blockers or required user actions.
- Do not repeat the issue description or list every unchanged requirement.
- Show detailed explanations, logs, or code walkthroughs only when requested or necessary.

### Testing Efficiency

- Run focused tests relevant to changed functionality first.
- Run broader tests, linting, or builds when required by the change or acceptance criteria.
- Avoid repeating checks when neither relevant code nor configuration has changed.
- Prefer concise test output.
- Investigate failures before rerunning commands.
- Never claim tests passed unless they were actually executed.

## 4. Architecture and Implementation

- Both game modes must share one server-authoritative gameplay engine.
- Validate gameplay actions and permissions on the backend.
- Separate frontend presentation, API communication, and gameplay logic.
- Use strict TypeScript and established C# conventions.
- Keep modules and services focused and maintainable.
- Avoid oversized components, duplicated functionality, unnecessary dependencies, and premature infrastructure.
- Reuse existing implementations when appropriate.
- Do not introduce new frameworks or architectural patterns without a demonstrated need.
- Treat V1 as reference only. Review, refactor, and test any migrated logic; never copy it blindly.
- Remove code only when confirmed unused.
- Preserve existing public interfaces unless the issue requires changing them.
- Add or update automated tests appropriate to the change.
- Do not change unrelated behavior.

## 5. Security and Data

- Never commit or expose secrets, credentials, API keys, or connection strings.
- Never reuse or reproduce the previously exposed V1 database credential.
- Treat rotation of that credential as unverified until evidence confirms otherwise.
- Validate all gameplay actions and permissions server-side.
- Treat client input as untrusted.
- Follow security and data-handling decisions documented in `docs/architecture.md`.

## 6. GitHub Issue Workflow

Work on one GitHub issue at a time.

### Before Implementation

1. Retrieve the complete issue description from `joelsjovall/WordGame2.0` when GitHub access is available.
2. Identify its Tasks, Acceptance Criteria, Dependencies, and relevant architectural requirements.
3. Inspect relevant project files and current Git status.
4. Create a short implementation plan only when the task is nontrivial.

If GitHub access is unavailable and the complete issue description is not already available, ask the user to provide it. Never guess issue requirements.

### During Implementation

- Implement only the active issue's requirements.
- Follow existing conventions and architecture.
- Preserve unrelated local changes.
- Do not discard, overwrite, or reset user changes without explicit authorization.
- Avoid unrelated cleanup or refactoring.
- Keep code changes minimal and readable.
- Add or update relevant tests.

### After Implementation

1. Run appropriate verification.
2. Check the implementation against all Acceptance Criteria.
3. Review the diff for unintended changes.
4. Briefly report completed work, verification results, incomplete requirements, and known limitations.
5. Never claim an issue is complete unless all Acceptance Criteria are satisfied.

## 7. Git and Branch Management

### Standard Workflow

1. Inspect the issue and local Git status.
2. Start from an updated `main` when safe.
3. Create a dedicated feature or documentation branch.
4. Implement and verify the changes.
5. Review the diff.
6. Commit and push.
7. Create a pull request.
8. Merge and close the issue when its Acceptance Criteria are satisfied and required checks and reviews have passed.

Track work in GitHub Projects:

`Backlog → In Progress → Done`

### Git Operations

- Codex may create branches, switch branches, commit, push, and create pull requests without requesting separate user approval.
- Codex may merge pull requests and close completed issues when all requirements and repository protections are satisfied.
- Follow repository branch protection rules and required reviews.
- Never force-push, hard-reset, or discard unrelated changes without explicit authorization.
- Never bypass required checks or approvals.
- Do not automatically merge incomplete or failing pull requests.
- Preserve unrelated work when creating or switching branches.
- Do not modify `main` directly when the repository workflow requires pull requests.

## 8. Documentation

- Update documentation only when actual decisions, behavior, architecture, or project status change.
- Avoid duplicating information across documentation files.
- Do not create unnecessary documentation files.
- Keep documentation concise and relevant.
- Do not update `docs/project-context.md` after every minor code change.
- Do not rewrite entire documentation files when a targeted edit is sufficient.

Documentation responsibilities:

- `AGENTS.md`: Permanent development instructions.
- `docs/architecture.md`: Technical architecture and architectural decisions.
- `docs/project-context.md`: Scope, roadmap, progress, and handoff information.

## 9. Scope Control

- Do not implement features outside the active issue.
- Do not introduce speculative improvements.
- Do not refactor unrelated code.
- Do not add dependencies without a clear need.
- Do not create unnecessary abstractions.
- Do not rewrite functioning systems without justification.
- Do not modify unrelated files solely for formatting.
- When discovering unrelated issues, mention them briefly without implementing fixes unless necessary for the active task.

## 10. Definition of Done

An issue is complete only when:

- All Acceptance Criteria are satisfied.
- Required functionality is implemented.
- Relevant tests and checks pass, or limitations are clearly reported.
- No known critical regressions are introduced.
- The final diff contains no unintended changes.
- Any required documentation is updated.

Do not mark issues Done or claim completion prematurely.

## 11. Default Response Style

Be concise, direct, and implementation-focused.

Prefer:

- Minimal necessary explanation.
- Focused code changes.
- Short verification summaries.
- Clear identification of blockers.

Avoid:

- Repeating instructions.
- Long introductions.
- Excessive progress narration.
- Unnecessary implementation summaries.
- Full file dumps when a diff or targeted edit is sufficient.
- Broad repository exploration without justification.

**Default principle: Read less, change less, explain less — while completing the task correctly.**
