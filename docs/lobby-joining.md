# Lobby joining and invitations

F024 adds account-free joining to F023's [private lobbies](private-lobbies.md). Open `/join` to enter a code and display name, or open `/join/{lobbyId}/{lobbyCode}` from a shared invitation. Opening a URL never changes membership; the player must submit the form. The response confirms membership and offers invitation copying and native sharing where supported.

## Server rules

`POST /api/v1/lobbies/join` accepts `{ "lobbyCode": "ABC234", "displayName": "Bob", "lobbyId": "<optional invitation UUID>" }`. The client first ensures a guest identity. The backend derives the player ID from the protected cookie; clients cannot nominate membership identities. JSON, origin checks, credentialed requests, and safe Problem Details follow the creation API's security policy.

- Codes normalize casing and outer whitespace. Display names are trimmed, nonblank, at most 32 characters, and contain no retained control characters.
- Names are unique within a lobby using ordinal case-insensitive comparison. Existing spelling is preserved. A different guest using an existing name receives 409.
- The same guest joining again with the same normalized name receives the current lobby without another slot, even when full. Changing that guest's name returns 409; joining is not a rename operation. Identity is browser-cookie based, so clearing cookies or using another browser creates another guest.
- The store serializes expiry, capacity, identity, and name checks with membership updates. Concurrent requests cannot overfill the four-player roster or duplicate a guest. Domain rules produce immutable snapshots; joins preserve the host, code, expiry, and creation receipts.
- Malformed input returns 400; missing identity returns 401; foreign origins return 403; unknown/expired codes or mismatched invitation IDs return 404; full lobbies and membership/name conflicts return 409. Errors never add a player.
- Expiry remains 30 minutes by default and is not renewed by joining. Invitations bind both the code and lobby ID, so a code reused after expiry cannot redirect an old invitation to a new room. Manually entering a code resolves its current active lobby.

## Browser behavior and limits

Invitation URLs contain public resource identifiers, never credentials. They use the frontend origin. Production static hosting must serve `index.html` for client routes, including direct invitation links. Deploy with HTTPS and same-site API hosting as described in the architecture.

The form prevents duplicate pending submissions, cancels work when leaving the page, and displays server errors with a retry path. Copy uses the Clipboard API; failures leave a selectable URL for manual copying. Native sharing appears only when `navigator.share` exists, with cancellation handled neutrally and copy available after failure. Clipboard/native share availability depends on the browser, secure context, and device.

The join confirmation is a response snapshot; its Open lobby settings link opens the [live host/settings view](lobby-settings.md). Lobby creation now also has a homepage entry. F026 adds [readiness and player management](lobby-players.md). Gameplay continues to use the shared server-authoritative engine.

## Acceptance review

| F024 criterion | Evidence |
| --- | --- |
| Join using a code | `/join`, authenticated join endpoint, real desktop/mobile tests. |
| Join using a shared URL | ID-bound invitation route and real browser direct-link/reload tests. |
| No registration required | Guest bootstrap before joining; separate browser guest tested against a real host lobby. |
| Reject full, expired, and invalid lobbies | Domain/store/API tests, exact expiry and code-reuse tests, clear UI errors. |
| Prevent multiple slots for the same player | Server-derived identity, immutable idempotent join, concurrent store tests and browser reload/rejoin. |
| Desktop/mobile invitation links | Both Playwright projects cover direct links, copy/share capability, and viewport overflow. Native OS share sheets are capability-stubbed, not exercised on physical devices. |
| Backend validates membership | Cookie authentication, strict request deserialization, origin checks, atomic identity/name/capacity validation. |
| Relevant automated tests pass | 45 focused backend tests, 16 frontend tests, six browser tests; builds, lint, formatting and generated-contract check. |

All eight criteria are implemented. The full regression suite remains part of PR CI. See [testing](testing.md) for commands.
