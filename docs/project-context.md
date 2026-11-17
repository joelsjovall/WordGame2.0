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
- **Next:** F002, Set up clean frontend project structure. Its intended scope is React + TypeScript + Vite initialization, project structure, routing, linting, strict TypeScript, demo removal, and a simple placeholder homepage. Implementation must wait for the user's instruction to continue.
- **Local state at inspection (2026-11-17):** Branch `feature/F002-frontend-setup`. An existing uncommitted change reduces `README.md` from the architecture text to `# WordGame 2.0`. Preserve this change; it predates this handoff. The tracked working files inspected were `README.md` and `docs/architecture.md`; no frontend or backend implementation was present.
- **This task:** Add `AGENTS.md` and this context document only. Do not implement F002, change branches, commit, or push. Future branch changes, commits, and pushes require asking the user first, as recorded in `AGENTS.md`.
- **Outstanding security follow-up:** A database credential was previously exposed in the public V1 repository. Never reuse or reproduce it. Rotation must be verified; this handoff provides no evidence that rotation has occurred.

Update this section as issues are completed or the handoff state changes.
