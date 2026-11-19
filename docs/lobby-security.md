# Private lobby security

F031 (#33) reviews the existing Party authorization boundaries and adds request throttling. Both modes continue to use the shared authoritative game engine.

## Identity and permissions

Guest identities are random server-issued IDs in protected ASP.NET Core authentication cookies. Production cookies are Secure, HttpOnly, SameSite=Lax, and expire after one day without sliding renewal. Request bodies and identity headers cannot choose the acting player. Unknown JSON fields are rejected. Lobby reads and SSE subscriptions require authenticated membership; writes verify membership and host permissions in server-side lobby operations. Host migration changes who may perform settings, start, rematch, and removal actions.

Removed members lose reads, new subscriptions, and all protected actions, and the same identity cannot rejoin that lobby. Existing streams recheck membership before sending and receive a terminal removal notification. Private game state/events also require authenticated game membership; knowing a game ID does not grant access. See [game synchronization](game-synchronization.md) and [lobby synchronization](lobby-synchronization.md).

Names are trimmed and constrained to nonblank text of at most 32 characters with no remaining control characters. Case-insensitive duplicate names are rejected. JSON transports names as data, and React renders them as escaped text; HTML-like names never become markup. Names are not authentication credentials.

JSON-only writes reject explicitly foreign browser origins; development exceptions use configured exact origins. Production requires same-origin hosting. Missing Origin headers do not grant identity or bypass server permissions.

## Request limits

The API uses the [built-in ASP.NET Core rate limiter](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0), with separate fixed one-minute windows and no request queue:

| Endpoint | Setting under `LobbyRateLimits` | Default permits/minute/IP |
| --- | --- | --- |
| POST `/api/v1/lobbies` | `CreatePermitsPerMinute` | 20 |
| POST `/api/v1/lobbies/join` | `JoinPermitsPerMinute` | 30 |
| POST `/api/v1/guests` | `GuestPermitsPerMinute` | 60 |

All configured limits must be positive. Rejections return 429 problem JSON with `Retry-After`. Valid, invalid, and missing-code requests consume the join budget. Limits use the connection IP (normalized to IPv6), not cookies, submitted IDs, code values, or caller-supplied forwarding headers, so changing identities or invitations does not reset the budget. Unknown connection IPs share one fallback partition. Authentication rejects anonymous protected requests before they reach the endpoint limiter.

Limits are local to the backend instance and reset on restart. Shared networks share a budget. A fixed window permits bursts around its boundary and is not distributed denial-of-service protection. The application does not trust forwarded headers; behind a reverse proxy, configure trusted proxy handling deliberately before relying on client-IP fairness. Do not trust arbitrary `X-Forwarded-For` values. Playwright's isolated test backend raises all three budgets to 1000 to accommodate parallel loopback clients; production defaults remain unchanged.

## Invitation review

Codes use cryptographically generated random six-character values, not sequential identifiers. There is no public lobby directory or code-lookup endpoint. Joining is rate limited; unknown codes and mismatched invitation IDs return the same generic 404 without a roster. Joining a valid room still intentionally grants player membership and can return capacity/name conflicts. This is an invitation mechanism, not an undiscoverable secret or host credential.

Links contain only the lobby ID and code. They never contain authentication cookies or player tokens. A link cannot exercise host privileges. The frontend now sets `no-referrer` to avoid sending invitation paths in outbound Referer headers. Anyone with a valid invitation can request a slot; treat sharing it as inviting a player. A removed player can create a fresh guest identity, so removal is session-based rather than a permanent person/IP ban. Closing/expiring the lobby invalidates its invitation.

## Acceptance review and validation

| Criterion | Evidence |
| --- | --- |
| Valid identity required | Anonymous, forged-cookie/header, and expired-cookie matrix across every lobby operation |
| Host-only actions authorized | Settings, start, rematch, and removal denied to guests and outsiders; existing host migration tests |
| No participant impersonation | Actor headers ignored, forged cookies rejected, submitted actor fields rejected |
| Codes do not grant administration | Valid invitation join grants membership only; host actions remain denied |
| Sensitive endpoints rate limited | Create/join/guest limits tested across changing identities, codes, and forwarding headers |
| Removed/unauthorized users cannot mutate | All six lobby actions denied after removal; rejoin denied |
| Updates stay private | Unauthorized state/SSE reads denied; existing stream revocation and private game subscription tests |
| Security tests pass | Eight new API security cases and frontend markup-rendering regression, alongside existing authorization tests |

The full API suite passed before the final additional revocation case; that case and all eight security cases then passed together. Forty focused frontend/API-client tests and fourteen Party browser cases passed, as did frontend lint, formatting, production build, and backend formatting. All eight criteria are satisfied within the documented single-instance, guest-session model.

The previously exposed V1 database credential is neither read nor reused here. Its rotation remains unverified; no evidence of rotation was supplied.
