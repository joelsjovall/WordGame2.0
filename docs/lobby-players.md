# Lobby players, readiness, and reconnection

F026 extends the [live lobby/settings screen](lobby-settings.md) with membership, presence, Ready/Not Ready, leave, and host removal. Both modes continue to use the same gameplay engine; these rules govern the private pregame lobby.

## Membership and readiness

Each roster member exposes a display name, host marker, isConnected, and isReady. The server derives the acting player from the protected guest cookie. Ready changes require the current canonical revision string, preventing a delayed Ready request from approving settings or a roster the player has not seen. Only a connected member can become ready. Repeating an already-applied ready value at the current revision is harmless.

Every member, including the host, must be connected and ready before startup. LobbyResponse.canStart is the backend's current readiness/count/lifecycle result; host authorization is still checked separately. Settings changes, new joins, departures, kicks, and host migration reset all readiness. Losing the last connection clears that player's readiness. Reconnecting does not automatically mark them ready.

New credentialed JSON commands:

| Route | Request | Authority |
| --- | --- | --- |
| POST /api/v1/lobbies/{lobbyId}/ready | `{ "expectedVersion": "8", "isReady": true }` | Current member, waiting lobby. False marks Not Ready. |
| POST /api/v1/lobbies/{lobbyId}/leave | `{}` | Current member, waiting lobby. |
| POST /api/v1/lobbies/{lobbyId}/remove | `{ "playerId": "<target UUID>" }` | Current host, waiting lobby; use Leave for oneself. |

Success returns the updated LobbyResponse. Writes enforce the existing JSON/origin policy; malformed input returns 400, absent identity 401, unauthorized membership/host actions 403, missing/expired lobbies 404, and stale revisions or lifecycle conflicts 409. Read and SSE access continue to require current membership.

## Connections, grace, and host migration

An authorized lobby SSE stream acquires a server-generated connection lease and releases it in a finally block. Several tabs share one member slot but hold independent leases. Closing one tab cannot mark another live tab disconnected. REST requests alone do not establish presence. A new/joining member has a grace window to open the live lobby screen.

After the last connection closes, the member keeps their identity, name, and slot for `Sessions:DisconnectedGrace` (default two minutes, shared with the existing game-session policy). A temporary host disconnect preserves hosting. Reopening the lobby URL with the same cookie reconnects that member. Network failure is detected when the SSE connection closes/fails; 15-second writes help detect broken connections. Grace starts at the server-observed disconnection, not an estimated client timestamp.

At `now >= disconnectedAt + grace`, reads, writes, or the existing cleanup sweep remove the disconnected waiting member. The sweep runs at `Sessions:CleanupInterval` (default one minute), so automatic cleanup may occur on the next sweep; a request enforces the boundary immediately. Explicit Leave skips grace. When the host permanently leaves, the earliest remaining connected member becomes host; if none are connected, the earliest remaining member holds hosting during their grace window. An empty lobby closes and releases its code and creation receipts. Started game rosters are never changed by waiting-lobby cleanup.

Kicked identities cannot rejoin that lobby, even with its code or invitation. Their reads/streams and all former lobby permissions are revoked. The stream rechecks current membership before each payload, including updates queued before removal, then emits removed and closes. The browser hides the former lobby controls. A voluntary or grace-expired departure is not a ban; that identity may join again as a new, unready member if the lobby remains available. Original creation receipts cannot disclose a room after the creator has left, and replay does not restore host authority after migration.

Guest identity is browser-cookie based. Clearing cookies or using another browser creates a new identity; a kick is not an account-level or person-level ban. There is no mandatory registration or new persistence dependency.

## Startup races

The store checks the host, revision, capacity, connected membership, and readiness under the same gate that reserves startup. If Leave/Remove wins first, the roster/revision changes and the old start request conflicts. If startup wins first, explicit leave/remove/ready/settings requests return 409 until it resolves; the UI hides waiting-room actions during startup.

Presence can still change while the shared engine creates the game. Completion rechecks the latest connection/readiness state, rather than the old reserved snapshot. A last-connection disconnect invalidates startup even if the member quickly reconnects, because readiness was cleared. The application abandons the created game, preserves current presence, returns the lobby to waiting, and resets all readiness. Players can explicitly ready again and retry. Once startup commits, the game roster/settings remain frozen; leaving an active game belongs to the later match lifecycle work.

## Acceptance review and checks

| F026 criterion | Evidence |
| --- | --- |
| All players see current members | Membership-protected state/SSE; names, host, presence, and readiness UI; three-player browser flow. |
| Ready states synchronize | Versioned Ready/Not Ready, invalidation rules, API tests and desktop/mobile updates. |
| Safe lobby departure | Atomic leave, empty-room closure, host migration, startup conflict policy. |
| Hosts can remove players | Host-only removal endpoint and UI; nonhost rejection tests. |
| Removed players lose former permissions | Membership checks, denied rejoin, SSE revocation, and hidden controls; API/browser tests. |
| Reconnect retains identity | Cookie identity plus independent connection leases; API/browser multi-tab/reconnect tests. |
| Host migration works | Explicit departure and exact grace-boundary tests, connected-member preference, creation receipt regression test. |
| Startup conditions satisfied | All-member ready/connected gate; stale writes rejected; reservation/disconnect race tests verify rollback and no active orphan game. |
| Relevant automated tests pass | 74 focused backend tests, 28 frontend tests, six desktop/mobile browser tests; builds, lint, formatting and generated-contract check. |

All nine criteria are implemented. Full regression remains part of PR CI. See [testing](testing.md) for suite commands.
