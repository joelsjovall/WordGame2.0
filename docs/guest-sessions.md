# Shared guest sessions

F033 (#35) extends the existing Party cookie authentication for both modes. There is one guest identity system; Online matchmaking itself remains later work.

## Identity and validation

`POST /api/v1/guests` with `{}` creates a server-generated random player ID and an independently generated session ID. Only the public player ID is returned. The session ID is carried inside the encrypted/authenticated ASP.NET Core cookie, never in an API response, URL, localStorage, or caller-provided player field. Production cookie settings remain Secure, HttpOnly, SameSite=Lax, and persistent with an absolute one-day lifetime.

`GuestSession` and `IGuestSessionStore` live in Application; Infrastructure supplies an in-memory implementation. Each session records player ID, issue/expiry times, and a revocation token. Cookie validation checks that the server still knows the session, it has not expired, and its player ID matches the protected claim. A signed cookie alone is insufficient after revocation or a backend restart. Protected lobby actions, game actions, state reads, and event subscriptions continue to use verified claims.

Repeated bootstrap calls and ordinary browser refreshes reuse a valid identity and do not issue a replacement cookie or extend expiration. There is no sliding renewal. Once expired, protected operations return 401; a subsequent explicit bootstrap issues a new identity. It cannot recover old membership or scores. Existing protected cookies without the new session claim are likewise rejected and require bootstrap after deployment.

## Reset and open connections

`POST /api/v1/guests/reset` with `{}` requires authentication and the existing same-origin write protection. It shares the guest endpoint's per-IP rate limit. Success returns 204, revokes that server session, and expires the browser cookie. Reset does not create a replacement identity automatically. A copied old cookie cannot be replayed, and other guests' sessions are unaffected.

Both lobby and game SSE streams link their lifetimes to the session's revocation token. Reset closes them and releases their presence leases; existing disconnection/grace/forfeit rules then apply. Reset is not an intentional lobby or game leave command. It cannot undo requests that already completed or restore the old guest later.

The frontend API exposes `guestsApi.reset()` alongside `ensure()`. It waits for an in-flight bootstrap before resetting; bootstrap calls made during reset wait for completion, avoiding a late sign-in overwriting the cleared cookie. Callers should leave active views when offering reset. This issue adds the API/client operation, not a user-facing account/settings screen.

## Display names

Display names remain match/lobby-scoped presentation, not credentials or global unique usernames. Party players choose a name when creating/joining; names are trimmed, nonblank, limited to 32 characters, and reject control characters. Case-insensitive duplicates within a lobby are rejected, and React renders names as text. An existing member cannot rename itself by replaying join with a different name.

Players may choose a different name for a new lobby or future Online queue entry. Mid-match renaming and a persistent global profile are not introduced here. Online queue handling must reuse this identity and the same validation policy when its endpoint is implemented.

## Lifetime and storage limits

Expired registry entries are removed when read or when another session is issued; revocation cancels their stream tokens. Store disposal revokes remaining entries. This is the same single-instance/in-memory deployment model as active games: restart loses guest sessions, lobbies, and matches. A future durable/multi-instance implementation must preserve expiry and cross-instance revocation rather than merely persisting cookie keys.

There are no persistent accounts, identity recovery after clearing cookies, or person-level bans. Anyone may intentionally start a new guest after resetting. See [lobby security](lobby-security.md) for rate limits, invitation behavior, origin checks, and the unverified V1 credential-rotation follow-up.

## Acceptance review

| Criterion | Evidence |
| --- | --- |
| Play without registration | Existing guest bootstrap and all Party browser flows retained |
| Unique verifiable identity | Independent player/session IDs, registry lookup, protected player-claim validation |
| Secure backend credentials | Random backend IDs inside protected HttpOnly/Secure cookies; no session credentials in public DTOs |
| No parameter impersonation | Existing strict JSON/identity matrix and verified claims retained; reset rejects supplied player fields |
| Survive ordinary refresh | Bootstrap reuse and existing browser reconnection/refresh cases |
| Reject expired/invalid sessions | Registry-clock expiry, protected-cookie expiry/forgery, and copied-cookie replay after reset tests |
| Shared Party/Online management | Same real guest cookies authorize Party and Online-format shared-engine games; reset closes both formats' streams |
| Security tests pass | New registry, HTTP reset/expiry/stream, and frontend bootstrap-order tests plus existing authorization regressions |

All eight criteria are implemented. Verification passed: 445 backend tests, 73 frontend tests, all 36 desktop/mobile browser cases, lint, formatting, strict TypeScript/production build, and diff checks. Online-format tests create games through the shared engine because queueing is not yet implemented; they do not claim a playable Online matchmaking UI.
