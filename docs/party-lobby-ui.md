# Responsive Party lobby

F029 (#31) improves the existing waiting room without changing API contracts or gameplay rules.

## Presentation and responsibilities

The lobby stacks invitation, player, and settings cards on phones and uses two columns from 48rem. Code and invitation inputs support manual selection alongside copy buttons. Player cards show names, host badges, connection state, and readiness. Host settings and match startup occupy a separate panel; guests see a read-only summary.

`LobbyPage` composes the screen. `InvitationPanel`, `LobbyMembers`, `LobbyMatchSettings`, and `LobbyScreenState` handle presentation; `useLobbyActions` handles pending writes and error recovery. `useLobby` accepts authoritative snapshots and derives join, leave, and host-change announcements through `lobbyActivity`. Older revisions cannot replace current state or create activity messages.

Readiness, permissions, settings, and start eligibility remain server-owned. Unsaved settings also disable the local start button with an explanation. Saving or receiving changed server settings resets that draft; rejected saves preserve it. Actions recover state with a read rather than retrying writes.

## Acceptance review

| Criterion | Evidence |
| --- | --- |
| Understandable navigation | Labelled cards, home/leave navigation, readiness hints, and disabled-start explanations |
| Copy codes and links | Separate clipboard controls and selectable fallback inputs |
| Accurate names and readiness | Authoritative player cards and ready action; component and browser coverage |
| Separate host controls | Host settings/start panel; guest read-only summary |
| Phone, tablet, desktop | Browser checks at 360, 768, and 1280 CSS pixels; long names, overflow, and 44px touch targets |
| Loading and error states | Loading skeleton, unavailable-room navigation, action alerts, and connection status |
| Backend authority | Existing API/SSE flow, revision guards, and server eligibility retained |
| Reusable structure | Focused presentation components and separate action/snapshot hooks |
| Frontend checks pass | 28 focused frontend tests, 6 responsive browser cases, 18 existing Party browser regressions; lint and production build |

All nine criteria are implemented. Phone and desktop screenshots were visually reviewed. Browser device emulation does not replace physical-device testing. Clipboard/native sharing depend on browser capabilities; manual copying remains available. Backend code is unchanged, so backend unit suites were not rerun for this issue.
