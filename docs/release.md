# Public beta readiness

**Decision as of 2026-11-25: No-go.** Do not tag or announce a public beta while the release blockers below remain unresolved.

## Beta scope and release notes draft

WordGame 2.0 offers private Party lobbies for 2–4 players and guest Online 1v1 matchmaking. Players select a category, bid, call bluffs, enter timed answers, score rounds, and play rematches. Both modes use the same server-authoritative game engine. The browser supports mobile and desktop layouts, live updates, reconnection, audio controls, and keyboard-accessible instructions. Online match history is stored in PostgreSQL when production is configured with Neon.

Ranked matchmaking, accounts, a large category catalogue, and native mobile packaging are outside this beta. The initial category dataset is small. On free Render hosting, idle wake-up can be slow, and deploys or restarts interrupt active games. Guest identities are in memory, so a restarted server cannot reconnect a returning guest to their previous statistics even though completed match records remain stored.

## Release gate

| Gate | Evidence and remaining action |
| --- | --- |
| Party and Online gameplay | Local Playwright journeys cover full Party matches with 2–4 players and a six-round Online match. The new timeout/duplicate-answer journey passed locally. Run the release-critical CI suite on the final PR. |
| Multiplayer recovery | Local browser journeys cover reconnecting, forfeits, rematches, and return to matchmaking. Verify a complete match in each mode against the deployed service using separate devices. |
| Build and CI | Local frontend and backend Release builds passed during development. Confirm all required checks on the final PR and main commit. |
| HTTPS and health | On 2026-11-25, `/health`, `/`, and `/api/v1/categories/` returned HTTP 200 with valid TLS on the Render URL. Verify production logs during live gameplay. |
| Secrets | **Blocked:** the credential previously exposed in public V1 must be confirmed revoked, rotated, or made invalid by deleting its database. Confirm GitHub native secret-scanning settings and resolve any alerts. Do not paste credentials into an issue or PR. |
| Statistics | Completed Online records use Neon in production. Verify a real completed match remains in Neon after a Render restart; confirm how a guest can reach prior statistics after restart before promising that feature. |
| Performance | [Local measurements](performance.md) completed 10 simultaneous Party and 10 simultaneous Online matches. Confirm sustained memory/CPU and whether the free Render instance supports the intended beta load. |
| Usability | Review the deployed game on a physical phone and desktop, including loading, errors, timers, and reconnection messages. Resolve any critical defects found. |

Open beta bugs as [GitHub issues](https://github.com/joelsjovall/WordGame2.0/issues) with device/browser, steps to reproduce, expected and actual result, and a screenshot when useful. Do not include cookies, database strings, or other secrets. See [deployment](deployment.md) for manual deploy and rollback, and [testing](testing.md) for local and CI test commands.

When every gate is satisfied, review this decision, create the release tag from the verified main commit, and publish the final notes. A failed deployment should be rolled back to the previous Render image; players in interrupted in-memory matches must start new sessions.
