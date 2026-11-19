# WordGame 2.0 – Architecture

## Overview

WordGame 2.0 is a browser-based multiplayer word-bidding and bluffing game built with React, TypeScript, and ASP.NET Core.

The application supports two game modes:

- **Party Mode:** Private matches for 2–4 players.
- **Online Mode:** Automatic matchmaking for 1v1 matches.

Both modes share the same server-authoritative gameplay engine.

This is the intended product architecture. The current implementation includes domain models/invariants, word validation and [timed challenge submissions](word-challenges.md), local JSON category storage, an in-memory session store, the shared Application [GameEngine lifecycle](game-engine.md), public category metadata endpoints, authenticated [state/SSE synchronization](game-synchronization.md), API contracts, a placeholder React frontend, tests, and CI. F023 adds [guest identity and private waiting-lobby creation](private-lobbies.md). F024 adds [lobby joining and invitations](lobby-joining.md), including a mobile-friendly join screen. F025 adds [host settings, lobby synchronization, and shared-engine startup](lobby-settings.md). F026 adds [lobby readiness, player management, reconnection and host migration](lobby-players.md). F027 adds [playable Party matches, countdowns, results and rematches](party-lifecycle.md). Online matchmaking and presentation polish remain planned. See [project context](project-context.md) for changing progress.

## Architecture

The application consists of four backend layers:

1. **Domain:** Gameplay entities, rules, state transitions, and scoring logic.
2. **Application:** Use cases, orchestration, game sessions, and multiplayer workflows.
3. **Infrastructure:** Category providers, in-memory storage, persistent repositories, and external integrations.
4. **API:** REST endpoints, SSE notifications, authentication, authorization, and application configuration.

The frontend uses React and TypeScript with feature-based organization.

## Gameplay

The shared game engine handles:

- Match lifecycle and round progression
- Player turns
- Category selection
- Bidding and bluff challenges
- Timed word submissions
- Word validation
- Scoring and win conditions

All gameplay actions are validated by the backend.

## Multiplayer

Party Mode and Online Mode share the same game session infrastructure.

Party Mode handles private lobbies, invitations, host permissions, and custom match settings.

Online Mode handles matchmaking, automatic match creation, and online-specific disconnect and forfeit rules.

Server-Sent Events notify connected clients about state changes. Clients can retrieve authoritative game state through the API, including after reconnection.

## Data Storage

Categories and accepted answers are initially loaded from local JSON datasets.

Active game sessions are initially stored in memory in a single backend instance. This introduces a known limitation: active matches are lost when the backend restarts.

Persistent storage will be introduced for player statistics and match history when those features are implemented.

Storage implementations must remain separate from gameplay rules.

## Security

- Players use securely issued guest identities.
- Protected actions require verified player identity.
- Host permissions are checked on the server.
- Lobby codes are not authentication credentials.
- Sensitive configuration is kept outside version control.
- Gameplay results cannot be modified directly by clients.

## Frontend

The frontend is organized by features, including gameplay, party lobbies, and matchmaking.

Reusable UI components and API communication are kept separate from feature-specific presentation.

The application follows a mobile-first approach and should remain suitable for future mobile app packaging.

## Foundation decisions

| Decision                                                       | Reason and consequences                                                                                                                                                                                               |
| -------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| One shared authoritative engine for both modes                 | Prevents rule drift and client-controlled outcomes. Mode-specific lobby/matchmaking workflows must call shared gameplay use cases rather than introduce a second rules engine.                                        |
| Separate Domain, Application, Infrastructure, and API          | Keeps gameplay testable without HTTP or storage. Provider/store interfaces let implementations change without moving business rules into adapters. See [backend boundaries](../backend/README.md#project-boundaries). |
| Local JSON categories behind a provider contract               | Supports validation without database setup or network calls. Datasets are validated and cached at startup; external providers can implement the same boundary later. See [category datasets](category-datasets.md).   |
| In-memory sessions on one backend instance                     | Avoids persistence/distributed coordination before it is needed. Restarts lose matches, and horizontal scaling requires a new storage/coordination design. See [session management](session-management.md).           |
| REST state plus SSE notifications; actions planned             | Server-to-client notifications fit the chosen transport while commands remain HTTP operations. Reconnection retrieves authoritative state; bidirectional WebSockets are not introduced without a new requirement.     |
| Separate versioned transport DTOs and generated frontend types | Avoids exposing mutable domain state or category answers and reduces contract drift. See [API contracts](api-contracts.md) for implemented endpoints and future specifications.                                       |

Record significant changes with their context, rationale, tradeoffs, and consequences as described in [CONTRIBUTING](../CONTRIBUTING.md#configuration-security-and-decisions). Keep this overview and focused documents consistent with implementation.

## Quality Standards

- Use strict TypeScript checking.
- Follow established C# coding conventions.
- Write unit tests for gameplay rules.
- Write integration tests for critical backend workflows.
- Use Playwright for essential multiplayer user journeys.
- Run builds, linting, and tests through GitHub Actions.
- Avoid duplicated logic, unused dependencies, and oversized components.

## Guiding Principles

1. One shared gameplay engine.
2. Server-authoritative game state.
3. Clear separation of concerns.
4. Minimal infrastructure until needed.
5. Reuse verified V1 functionality selectively.
6. Keep the codebase understandable and testable.
7. Prioritize correctness and maintainability over premature optimization.
