# WordGame 2.0 project context

## Purpose and sources

WordGame 2.0 is a multiplayer word-bidding and bluffing game. Players select categories, bid on how many valid words they can name, raise bids or call bluffs, and complete timed word challenges. The game manages validation, turns, scoring, round progression, and match results.

- Active repository: https://github.com/joelsjovall/WordGame2.0
- V1 reference only: https://github.com/joelsjovall/wordgame
- [Architecture](architecture.md) is the source of truth for technical responsibilities and constraints.
- [Development instructions](../AGENTS.md) define implementation standards and the issue workflow.

This handoff captures the project decisions supplied by the owner on 2026-11-17. Roadmap and progress below have not been independently checked against live GitHub issues or Projects.

## Product scope

**Party Mode:** Private lobbies for 2-4 players, lobby codes and invitation links, host controls, configurable match settings, readiness, reconnection, and rematches.

**Online Mode:** Random 1v1 matchmaking, guest identities without mandatory registration, automatic match creation, disconnect handling, forfeits, and rematches. Persistent statistics will follow when implemented. Ranked matchmaking is optional and not a launch priority.

The browser experience is mobile-first, with possible future mobile app packaging.

## Technical baseline

The agreed stack is React, TypeScript, and Vite on the frontend, with an ASP.NET Core Web API backend split into `WordGame.Api`, `WordGame.Application`, `WordGame.Domain`, and `WordGame.Infrastructure`.

Both modes share a server-authoritative gameplay engine and session infrastructure. Real-time updates use Server-Sent Events (SSE). Categories and accepted answers initially use local JSON files, with future external providers supported through separate implementations. Active sessions initially live in memory on one backend instance; restarts lose active matches. Persistent storage is introduced when statistics and match history require it.

See [architecture](architecture.md) for the detailed layer boundaries, reconnection model, and security requirements. Planned verification uses xUnit, frontend component tests, Playwright, and GitHub Actions.

## Roadmap

The planned backlog contains 57 issues across six milestones. Work proceeds one issue at a time, using Backlog -> In Progress -> Done in GitHub Projects.

| Milestone | Issues |
| --- | --- |
| 0. Project Foundation | F001-F012 |
| 1. Core Gameplay | F013-F022 |
| 2. Party Mode | F023-F032 |
| 3. Online Multiplayer | F033-F042 |
| 4. UI, UX & Game Polish | F043-F052 |
| 5. Testing & Public Release | F053-F057 |

## Current handoff

- **Completed:** F001, Define WordGame 2.0 architecture, merged through PR #60. Local Git history confirms merge commit `f633f77`; the documentation is in `docs/architecture.md`.
- **Completed:** [F002, Set up clean frontend project structure (#4)](https://github.com/joelsjovall/WordGame2.0/issues/4), merged through PR #61 (merge commit `d46d640`); the issue is closed as completed. See `frontend/README.md` for setup, structure, and the documented development dependency audit limitation.
- **Completed:** [F003, Set up ASP.NET Core backend solution (#5)](https://github.com/joelsjovall/WordGame2.0/issues/5), merged through PR #62 (merge commit `54dc96f`); the issue is closed as completed. See `backend/README.md` for setup.
- **Completed:** [F004, Define shared gameplay domain models (#6)](https://github.com/joelsjovall/WordGame2.0/issues/6), merged through PR #63 (merge commit `ac4e6f2`); the issue is closed. See [domain models](domain-models.md) for invariants and deferred policies.
- **Completed:** [F005, Review and migrate reusable V1 game logic (#7)](https://github.com/joelsjovall/WordGame2.0/issues/7), merged through PR #64 (merge commit `3c07eac`). See the [V1 migration review](v1-migration-review.md) for the pinned-source review and reuse decisions.
- **Completed:** [F006, Design category and word validation system (#8)](https://github.com/joelsjovall/WordGame2.0/issues/8), merged through PR #65 (merge commit `25c733f`). See [category validation](category-validation.md) for the shared rules and contracts.
- **Completed:** [F007, Set up JSON-based category storage (#9)](https://github.com/joelsjovall/WordGame2.0/issues/9), merged through PR #66 (merge commit `1f9409d`). See [dataset setup](category-datasets.md) for the schema, provider, caching, and initial data.
- **Completed:** [F008, Design multiplayer game state management (#10)](https://github.com/joelsjovall/WordGame2.0/issues/10), merged through PR #67 (merge commit `ec7bede`). See [session management](session-management.md) for isolation, retention, and restart limitations.
- **Completed:** [F009, Define shared API contracts (#11)](https://github.com/joelsjovall/WordGame2.0/issues/11), merged through PR #68 (merge commit `2896d8c`). See [API contracts](api-contracts.md) for implemented metadata endpoints and future route specifications. Game/lobby/action/SSE/matchmaking handlers and identity remain later work.
- **Active:** [F010, Set up automated testing projects (#12)](https://github.com/joelsjovall/WordGame2.0/issues/12). Existing xUnit projects are reused with a shared API factory; Vitest component/client tests, Playwright desktop/mobile smoke tests, and suite commands are implemented locally. See [automated testing](testing.md). GitHub Actions wiring remains a later issue.
- **Current local state (2026-11-18):** The user authorized preparing `feature/F010-automated-testing` from updated `main` and continuing implementation. Committing, pushing, and merging require user authorization.
- **Outstanding security follow-up:** A database credential was previously exposed in the public V1 repository. Never reuse or reproduce it. Rotation must be verified; this handoff provides no evidence that rotation has occurred.

Update this section as issues are completed or the handoff state changes.
