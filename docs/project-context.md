# WordGame 2.0 project context

## Purpose and sources

WordGame 2.0 is a multiplayer word-bidding and bluffing game. Players select categories, bid on how many valid words they can name, raise bids or call bluffs, and complete timed word challenges. The game manages validation, turns, scoring, round progression, and match results.

- Active repository: https://github.com/joelsjovall/WordGame2.0
- V1 reference only: https://github.com/joelsjovall/wordgame
- [Architecture](architecture.md) is the source of truth for technical responsibilities and constraints.
- [Development instructions](../AGENTS.md) define implementation standards and the issue workflow.

This handoff records the project decisions supplied by the owner on 2026-11-17. Completed progress is checked against GitHub and Git history as issues finish; the full roadmap and GitHub Projects cards have not been independently audited.

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

| Milestone                   | Issues    |
| --------------------------- | --------- |
| 0. Project Foundation       | F001-F012 |
| 1. Core Gameplay            | F013-F022 |
| 2. Party Mode               | F023-F032 |
| 3. Online Multiplayer       | F033-F042 |
| 4. UI, UX & Game Polish     | F043-F052 |
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
- **Completed:** [F010, Set up automated testing projects (#12)](https://github.com/joelsjovall/WordGame2.0/issues/12), merged through PR #69 (merge commit `63fca23`). See [automated testing](testing.md) for the shared API factory, Vitest, Playwright, and suite commands.
- **Completed:** [F011, Configure GitHub Actions CI pipeline (#13)](https://github.com/joelsjovall/WordGame2.0/issues/13), merged through PR #70 (merge commit `5ff4285`). Hosted PR and push-to-main CI both passed all six jobs. See [continuous integration](ci.md) for maintenance, required-check setup, and the temporary advisory exception.
- **Completed:** [F012, Create project documentation and coding standards (#14)](https://github.com/joelsjovall/WordGame2.0/issues/14), merged through PR #71 (merge commit `202f6b5`). Foundation milestone issues F001–F012 are merged. See the root overview, contribution guide, setup, and coding standards for onboarding.
- **Completed:** [F013, Implement core game engine and lifecycle (#15)](https://github.com/joelsjovall/WordGame2.0/issues/15), merged through PR #72 (merge commit `ed054aa`). See [game engine](game-engine.md) for shared orchestration and trusted application inputs.
- **Completed:** [F014, Implement player turn management (#16)](https://github.com/joelsjovall/WordGame2.0/issues/16), merged through PR #73 (merge commit `faff5b5`). See [turn management](turn-management.md) for eligibility, rotating starters, replay guards, and disconnect/leave policy.
- **Completed:** [F015, Implement category selection and difficulty system (#17)](https://github.com/joelsjovall/WordGame2.0/issues/17), merged through PR #74 (merge commit `42696ae`). See [category selection](category-selection.md) for provider-backed snapshots, difficulty configuration, and timeouts.
- **Completed:** [F016, Implement bidding and bluff-calling system (#18)](https://github.com/joelsjovall/WordGame2.0/issues/18), merged through PR #75 (merge commit `ac46ef7`). See [bidding](bidding.md) for the shared rules and concurrency guarantees.
- **Completed:** [F017, Implement timed word challenge and answer submission (#19)](https://github.com/joelsjovall/WordGame2.0/issues/19), merged through PR #76 (merge commit `2e0f6b8`). See [word challenges](word-challenges.md) for cutoffs and result semantics.
- **Completed:** [F018, Implement scoring and points system (#20)](https://github.com/joelsjovall/WordGame2.0/issues/20), merged through PR #77 (merge commit `fa22249`). See [scoring](scoring.md) for finalized rules and atomic awards.
- **Completed:** [F019, Implement round results and progression (#21)](https://github.com/joelsjovall/WordGame2.0/issues/21), merged through PR #78 (merge commit `64999a0`). See [round results](round-results.md) for retained results and guarded progression.
- **Completed:** [F020, Implement match completion and win conditions (#22)](https://github.com/joelsjovall/WordGame2.0/issues/22), merged through PR #79 (merge commit `875a7d7`). See [match completion](match-completion.md) for confirmed formats, final standings, and ties.
- **Completed:** [F021, Implement real-time game state synchronization (#23)](https://github.com/joelsjovall/WordGame2.0/issues/23), merged through PR #80 (merge commit `2afdae7`). See [game synchronization](game-synchronization.md) for protected reads, subscriptions, recovery, and deadlines.
- **Completed:** [F022, Refactor and test core gameplay engine (#24)](https://github.com/joelsjovall/WordGame2.0/issues/24), merged through PR #81 (merge commit `b26edef`). The Core Gameplay milestone F013–F022 is merged; see [core gameplay review](core-gameplay-review.md).
- **Completed:** [F023, Implement private game lobby creation (#25)](https://github.com/joelsjovall/WordGame2.0/issues/25), merged through PR #82 (merge commit `17ce1f8`). See [private lobbies](private-lobbies.md) for guest identity and creation.
- **Completed:** [F024, Implement lobby join and invitation system (#26)](https://github.com/joelsjovall/WordGame2.0/issues/26), merged through PR #83 (merge commit `5063128`). See [lobby joining](lobby-joining.md).
- **Completed:** [F025, Implement lobby settings and host controls (#27)](https://github.com/joelsjovall/WordGame2.0/issues/27), merged through PR #84 (merge commit `591389c`). See [lobby settings](lobby-settings.md).
- **Active:** [F026, Implement lobby player management and ready system (#28)](https://github.com/joelsjovall/WordGame2.0/issues/28). See [lobby players](lobby-players.md) for readiness, connection leases, departures/removal, host migration, and startup race policy.
- **Current local state (2026-11-19):** Working on `feature/F026-lobby-player-management` from updated `main`. Commit and push require user authorization.
- **Outstanding security follow-up:** A database credential was previously exposed in the public V1 repository. Never reuse or reproduce it. Rotation must be verified; this handoff provides no evidence that rotation has occurred.

Update this section as issues are completed or the handoff state changes.
