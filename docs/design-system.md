# WordGame visual system

Direction: a friendly tabletop word game, with warm paper surfaces, evergreen ink/actions, restrained amber accents, and rounded letter-tile shapes. Prioritize readable game state over decoration. No external fonts, icon package, or CSS framework is required.

`frontend/src/styles/tokens.css` owns the palette and scales; `controls.css` owns shared controls/states. Existing feature CSS handles layout and consumes those tokens. Shared React `Button`, `Card`, `Badge`, and labelled `TextField` live in `src/components/ui.tsx`; native `dialog` uses the same surface tokens. Do not add a second theme or hard-code replacement palettes in features.

| Role | Tokens / use |
| --- | --- |
| Primary | `--color-primary`, `--color-on-primary`: evergreen actions with white text |
| Secondary | `--color-secondary`, `--color-on-secondary`: amber actions with dark text |
| State feedback | `--color-success`, `--color-warning`, `--color-error`, each with a `-soft` surface; always include explanatory text |
| Surfaces / text | `--color-page`, `--color-surface`, `--color-subtle`, `--color-ink`, `--color-muted`, `--color-line` |
| Typography | System rounded body; locally available Trebuchet display headings; monospace codes. Body 1rem/1.5, small text .875rem, section headings 1.25rem+, fluid main titles |
| Spacing | `--space-1` through `--space-7`: .25, .5, .75, 1, 1.5, 2, 3rem. Fluid padding may interpolate between scale values |
| Shape / depth | `--radius-control`, `--radius-card`, `--radius-pill`; `--shadow-card` for surfaces, `--shadow-raised` for hover |

Buttons have primary/secondary/danger variants, explicit disabled treatment, visible pressed/hover movement, and a 3px keyboard-focus outline. `busy` disables activation, retains the accessible name, exposes `aria-busy`, and adds a decorative spinner; use visible progress wording too. Form errors need text linked with `aria-describedby` and `aria-invalid`; placeholder text is not a label. Badges supplement text rather than conveying meaning by color alone.

Use native `dialog.showModal()` for modal focus containment/inert background and Escape behavior. Supply an accessible heading/name, an explicit close button, and restore focus to the invoker. Never replace native dialog with a visually positioned div. How to Play uses this pattern on the homepage and during gameplay.

Layouts start at 320px, remain single-column on phones, and may use two columns from 48rem (tablet). Use 64rem for additional desktop space only where needed; the existing 24rem rule handles very narrow invitation rows. CSS custom properties cannot be substituted into media-query conditions, so keep these documented values literal. Test at 360, 768, and 1280px with long names, zoom/reflow, and keyboard navigation.

Use at least 44px interaction targets (shared controls default to 48px). Semantic text pairs target WCAG AA 4.5:1 and control boundaries/focus indicators 3:1. Disabled controls are visibly inactive rather than a substitute for explaining unavailable actions. Honor `prefers-reduced-motion`: remove decorative animation/transitions, keep visible loading/status text, and never gate actions on animation completion. Transitions use 140–220ms with the shared easing; avoid flashing or screen-wide motion.

Icons use inline SVG, a 24-unit view box, 2-unit rounded strokes, `currentColor`, and `.wg-icon`; decorative icons use `aria-hidden`, while icon-only controls require a name. Keep icons paired with text for important game actions. The homepage arrow is the initial reference.

Browser checks cover responsive layouts, contrast pairs, keyboard focus, native dialog dismissal, touch size, and reduced motion. Component tests cover busy-action guarding and labelled error descriptions. Later UI issues can extend screens using this foundation.

Gameplay feedback follows authoritative state: phase/round entry, bids, bluff calls, word results, changed scores, and final winners use short transform effects. Text stays fully opaque throughout entry animations to preserve contrast. The challenge countdown briefly emphasizes each of its final ten seconds; the warning color and underline remain without motion. Winner stars animate once for 440ms; other effects take 140–220ms. Existing loading and matchmaking indicators are reused. No action or server timer waits for an animation, and reduced motion removes all effects while preserving text/status feedback. Mobile browser checks include reduced motion and Chromium CPU throttling; physical-device smoothness remains part of release validation.

## Accessibility audit (F050)

Automated checks use `@axe-core/playwright` with WCAG 2 A/AA, 2.1 A/AA and 2.2 AA tags. Coverage includes homepage, join/create forms, How to Play, waiting lobby, matchmaking/search/match found, category selection, bidding, challenge and final Party results in desktop/mobile Chromium and iPhone-emulated WebKit. Existing contrast, reduced-motion, 200% text, compact keyboard viewport and touch-target checks remain in place. No axe rules are suppressed. Run `npm exec -- playwright test accessibility.spec.ts mobile-layout.spec.ts onboarding.spec.ts design-system.spec.ts party-lifecycle.spec.ts online-matchmaking.spec.ts` from `frontend`.

Audit fixes: a visible-on-focus skip link and route focus recovery; focus recovery when a server transition removes a gameplay control; persistent round/turn and matchmaking announcements; concise accepted/rejected/duplicate feedback instead of rereading the whole answer list; labelled word-entry help; polite ten/five-second and expiry announcements instead of speaking every tick. Result text, statuses and urgency underline remain understandable with sound muted and motion disabled. Dialogs retain their native keyboard behavior and do not pause the server.

**Manual validation status:** no human screen-reader or physical-device session has been performed for F050. Scripted keyboard/focus and accessibility-tree checks are automated evidence, not a listening test or a WCAG conformance certification. Before closing F050, record tester/date/browser/reader and pass/fail results for:

1. NVDA with Firefox or Narrator with Edge: skip to content, join/create a lobby, edit settings and ready using keyboard only.
2. Search, hear match found/opponent/acceptance status, and accept/decline without a mouse.
3. Select a category, bid/call bluff, hear turn changes, and submit accepted, invalid and duplicate answers without losing input focus.
4. Hear the remaining-time warnings and round/match outcome without repeated answer-history or every-second announcements.
5. Open/close How to Play using keyboard and Escape; verify background controls are unavailable and focus returns. Repeat with VoiceOver/Safari.
6. Check 200% text and real browser zoom, reduced motion, muted audio and touch interaction on a physical phone.

**Known timing barriers:** Party hosts can select 5–300-second challenges before starting; Online challenges default to 30 seconds. Live challenges cannot be paused or extended individually. Online acceptance and reconnection windows are also fixed, and round summaries advance after three seconds (totals remain visible). These can disadvantage slower input or assistive technology users. The [WCAG timing criterion](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable) has real-time/essential exceptions, but their applicability has not been independently assessed here. Manual timing/announcement review remains open; do not claim full WCAG 2.2 AA compliance or close F050 on axe results alone. See [Playwright's testing guidance](https://playwright.dev/docs/accessibility-testing) on automated coverage limits.

## Optional game audio

The global footer supplies mute/unmute and volume controls. Sounds default to muted at 35% volume; preferences are saved under `wordgame.audio.v1` in local storage when available. Each tab/refresh requires an explicit Enable sounds click before creating/resuming Web Audio, even when an unmuted preference was saved. Unsupported or blocked audio leaves gameplay fully usable. Hidden tabs stop current sounds and drop new ones.

`src/services/gameAudio.ts` synthesizes original short sine/triangle cues for categories, bids, bluff calls, accepted/invalid/duplicate words, a one-time final-five-second warning, round completion, and winner-only victory. Source/licensing: these tones were authored for WordGame 2.0 in its source code; no third-party recordings, samples, or audio assets are distributed. No background music or audio dependency is included. All game information remains available visually.

Server-state transitions trigger one prioritized cue; reconnect snapshots do not replay missed events. Event IDs prevent duplicate cues, new cues replace active voices, volume/mute changes stop current playback, and effects never delay actions. Chromium tests exercise Web Audio; Windows WebKit lacks that API and verifies the fallback. Physical-device listening/volume comfort remains release validation.

## Mobile viewport and packaging

The viewport permits zoom, opts into safe-area layout, and requests content resizing for interactive widgets where supported. Body padding consumes `safe-area-inset-*`; inputs use at least 16px text. Pages remain naturally scrollable without fixed-screen locks. Challenge entry observes [VisualViewport](https://developer.mozilla.org/en-US/docs/Web/API/VisualViewport) resize/focus events and, in a short visible viewport, brings the timer, progress, field, and submit action into a compact sticky group. Word entry disables autocorrect/capitalization and supplies a Send key hint; Enter uses the same form submission. Zoom/orientation changes do not reset game state. See [viewport metadata](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/meta/name/viewport) for browser-dependent widget behavior.

Chromium and iPhone-emulated WebKit automation exercise real gameplay; keyboard tests explicitly contract the viewport because automation does not open native OS keyboards. This is engine/emulation coverage, not physical iOS Safari/Android Chrome certification. Before release or app packaging, check native keyboard/IME behavior, real browser zoom, notch insets, rotation, background/resume SSE recovery, and cookie persistence on representative devices/WKWebView/Android WebView. No native wrapper or orientation lock is introduced here. Prefer HTTPS/HTTP2 for deployment because [HTTP/1.1 SSE connection limits](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events) can stall multiple tabs; multi-player WebKit fixtures use separate browser processes to represent independent devices.
