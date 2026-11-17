# WordGame 2.0 – Architecture

## Overview

WordGame 2.0 is a browser-based multiplayer word-bidding and bluffing game built with React, TypeScript, and ASP.NET Core.

The application supports two game modes:

- **Party Mode:** Private matches for 2–4 players.
- **Online Mode:** Automatic matchmaking for 1v1 matches.

Both modes share the same server-authoritative gameplay engine.

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
