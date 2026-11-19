# Party Mode review and beta handoff

F032 (#34) reviews F023-F031 before Online Multiplayer development. Party Mode is ready for controlled beta testing on the supported single-instance setup, subject to the operational limits below. This is not a public deployment or a claim of production readiness.

## Review and cleanup

Reviewed lobby creation/joining, waiting-room presentation, settings/readiness, results, lobby/game synchronization, and their action hooks. Backend review covered lobby application services, lifecycle worker, session-store startup reservation/subscriptions, endpoint authorization, and domain roster rules.

`PartyResults` duplicated pending-write guards, errors, and authoritative recovery from `useLobbyActions`. It now uses that existing hook, removing the duplicate implementation and standardizing failure messages with the waiting room. Creation/join forms retain their separate abortable submissions and stable creation request IDs; those have different navigation and retry responsibilities. Lobby and game streams retain separate payloads and presence leases while sharing the recovery scheduler. Combining them would change established behavior without a demonstrated benefit.

No confirmed unused production feature, obsolete development mock, or debug statement was found in the reviewed Party paths. Test fixtures remain test-only. No unrelated code was removed. Backend gameplay, settings, identity, membership, host permissions, and transitions remain authoritative; UI checks express eligibility and pending state rather than replacing backend decisions. No backend production changes or new dependencies were needed.

## Coverage and acceptance review

| Criterion | Evidence |
| --- | --- |
| Full matches with 2-4 players | Playwright lifecycle scenario parameterized for 2, 3, and 4 distinct browser identities on desktop/mobile; four-round API regression verifies every player starts once and earns points |
| Safe join/disconnect/reconnect/leave | Creation, joining, player management, and Party reconnection suites; invalid/expired/full lobbies covered by API tests |
| Correct host permissions/settings | Security matrix and host tests; four-player completed match rejects guest rematch while accepting host rematch |
| Synchronized updates | Multi-client SSE tests and browser roster/readiness/settings assertions |
| Rematch in the same lobby | Every browser roster completes two games with different game IDs; retained settings and fresh scores/readiness verified |
| Remove confirmed unused/duplicated code | Duplicate results action handler removed; review found no other confirmed unused Party code |
| No known critical gameplay regressions | Full Domain, Application, Infrastructure, API, frontend, and browser suites pass |
| Automated checks pass | See verification below |
| Ready for beta testing | Playable creation-to-results flow, documented setup and reporting checklist, explicit deployment limitations |

The API four-player regression spans four real rounds through public actions and controlled server time, rather than simulating completion in the UI. The existing two-player API regression verifies retained results, game cleanup, and fresh rematch state. Browser tests complete a configured one-round match and a rematch for each supported roster size. Permission regressions exercise real authenticated requests; UI visibility is not treated as authorization.

## Verification

- Backend: 132 Domain, 133 Application, 86 Infrastructure, and 87 API tests pass (438 total), including existing Core Gameplay coverage.
- Frontend: 72 tests pass; lint, formatting, strict TypeScript/production build pass.
- Browser: all 36 cases in the complete desktop/mobile Chromium suite pass, including six lifecycle cases for 2/3/4 players.
- Backend solution formatting and Git diff whitespace checks pass.

## Running the beta

Follow [development setup](development-setup.md) to run the API and Vite frontend. Each participant needs a distinct browser profile/private session or device; tabs in one profile intentionally share a guest identity. Create a private lobby, invite two to four people, configure settings, ready everyone, and start. After results, the host can choose Play Again without creating another lobby.

For a short manual session, try joining by code and link, changing settings, unreadying, refreshing/reconnecting, leaving as host, completing a match, and rematching. Capture device/browser, roster size, lobby settings, action sequence, expected/actual behavior, and displayed error text when reporting a defect. Do not include guest cookies or other credentials. Use Playwright's local HTML report for automated failures; see [testing](testing.md).

## Limits and follow-ups

- Sessions and subscriptions live on one backend instance; restarts lose them. Multiple backend replicas are unsupported.
- Supported automation uses desktop Chromium and Pixel 5 emulation. Physical devices, Safari, Firefox, accessibility review with assistive technology, and real network conditions remain beta validation work.
- Guest identity is browser-session based; removed players can obtain a new identity. Per-IP fixed-window limits share budgets across a network and require deliberate trusted-proxy configuration when deployed behind a proxy. See [lobby security](lobby-security.md).
- Public hosting, TLS/proxy configuration, production monitoring/load testing, and persistence are not supplied by this issue. Online matchmaking remains future work.
- Previously exposed V1 credential rotation remains unverified. The current local JSON/in-memory game does not use that credential.
