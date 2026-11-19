# Party disconnection and recovery

F028 completes Party reconnection on top of F008 session storage, F021 state/SSE synchronization, F023 guest identity, and F026 lobby membership. It reuses the shared engine and store; there is no second client game state or reconnect-specific gameplay implementation.

## Identity and authoritative restoration

Refreshing or reopening `/lobbies/{lobbyId}/game` ensures the existing protected guest cookie, reads the current authorized lobby, and resolves its current game ID. It does not call Join or allocate a player slot. Game state/SSE restores the authoritative roster, round, turn, category, bids, challenge submissions, scores and deadlines. Several tabs share a player identity and maintain independent server-issued connection leases; closing one cannot disconnect another.

Only the original guest cookie can reclaim that identity. Missing/expired credentials, removed membership, unknown resources, backend restart, and retention cleanup do not trigger a guessed identity or automatic rejoin. Terminal 401/403/404 responses stop recovery, clear or hide the private view, and offer a route back to the homepage. Lost/cleared cookies cannot restore the old identity without that credential.

## Grace, timers, and continuation

| Configuration | Default | Purpose |
| --- | --- | --- |
| Sessions:DisconnectedGrace | Two minutes | Shared gameplay eligibility after the final game stream disconnects; nonnegative. |
| Sessions:LobbyDisconnectedGrace | Two minutes | Positive waiting/results lobby membership grace, independent of game cleanup configuration. |
| Sessions:CleanupInterval | One minute | Periodic shared session/lobby reconciliation and retention cleanup. |

Last game-stream disconnection publishes presence and a reconnectUntil timestamp. Within gameplay grace, reconnect restores the same score/round/turn and isConnected status. ReconnectUntil is a public display deadline, not a client permission token. Other members see Connected, Reconnecting with a grace estimate, or Left match. A last-tab disconnect and each new connection update authoritative state through existing SSE invalidations.

Server category and challenge timers continue during interruption. Deadlines, accepted words and prior scores never reset because a browser refreshed. Challenges can expire while the bidder is offline; state recovery shows the actual outcome. Controls are disabled while the client is recovering, and every eventual command still requires the server's current revision and turn/phase validation.

The existing engine reconciles expired eligibility during commands, timeout handling and cleanup. F028 additionally reconciles before adding a reconnecting stream, preventing a last-moment connection from bypassing elapsed grace before cleanup runs. Fewer than two eligible participants, or expiry of an essential highest bidder/caller, abandons the match without inventing a forfeit score. With enough eligible players, a nonessential expired turn is skipped. That player may later return to a still-active match with the same identity/score, but cannot restore the skipped turn. Ended sessions remain read-only until retention cleanup; reconnecting cannot reactivate them. The Party lifecycle returns ended matches to results/rematch handling.

## Intentional departure

GameActionRequest now also accepts `{ "type": "leaveMatch", "actionId": "<uuid>", "expectedVersion": "<revision>" }` through the existing protected action endpoint. It invokes GameEngine.LeaveAsync, immediately marks the player as departed, and reconciles turns without a grace delay. Missing/spoofed identities, stale revisions and invalid phases follow the existing action error policy. Departure never deletes scores or the game's immutable roster.

Public GamePlayerResponse adds hasLeft and nullable reconnectUntil so temporary interruption and intentional departure remain distinct. A departed player may observe retained state/results, but reopening a stream cannot add presence or restore action eligibility. The UI removes gameplay/leave controls for that match. Active lobby membership remains preserved; after completion the player may leave the lobby or join a newly readied rematch, as defined by the [Party lifecycle](party-lifecycle.md).

## Browser recovery

Lobby and game services share a single-timer recovery scheduler: retries begin at two seconds and back off to a maximum of thirty seconds. A live stream resets backoff; terminal access loss or unmount permanently stops it. Healthy streams do not poll. Closed/unavailable streams can be reopened and refresh authorized state. Failed initial guest/bootstrap requests also retry rather than leaving the page stuck.

Browser offline events immediately show recovery and disable match commands; online events trigger immediate refresh/reopening without waiting for the next backoff. Native EventSource reconnects are still supported. Connection callbacks are tied to their current source, older revisions are ignored, and late responses cannot restore a removed/expired view. Network failure detection on the server depends on the SSE/TCP connection; its fifteen-second heartbeat helps expose broken streams. No player score, round or timer is reconstructed from client storage.

## Acceptance review

| F028 criterion | Evidence |
| --- | --- |
| Refresh retains session | Desktop/mobile reload after earning points restores the same game, player, score and round. |
| Temporary interruption recovers | Real browser offline/online journey observes a missed opponent bid and resumes the correct turn. |
| Original identity and score | Guest-cookie SSE integration tests and browser state assertions; no join during recovery. |
| No duplicate entries | Fixed game roster, independent connection leases, multi-tab tests and unchanged lobby player IDs. |
| Accurate connection status | Presence invalidations, isConnected/hasLeft/reconnectUntil, observer browser/API assertions. |
| Disconnection cannot freeze play | Deadline preservation, challenge expiry, expired-grace abandonment/turn skipping, and late-connection guard tests. |
| Invalid/expired sessions handled | Existing credential/membership checks, stopped retries, terminal-view tests, and expired-cookie browser journey. |
| Automated coverage | Engine, real guest/SSE, frontend recovery/backoff, controls, and desktop/mobile browser checks. |

Local checks passed: 133 shared Application tests, 24 focused API tests, one store regression test, 30 frontend tests, and six desktop/mobile browser tests, plus builds, lint, formatting and generated-contract verification.

All eight criteria are implemented. Full regression checks remain part of PR CI; [testing](testing.md) lists suite commands. State and credentials remain subject to the existing single-instance/in-memory and cookie-lifetime limits.
