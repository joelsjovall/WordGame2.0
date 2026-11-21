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

Use native `dialog.showModal()` for modal focus containment/inert background and Escape behavior. Supply an accessible heading/name, an explicit close button, and restore focus to the invoker. Never replace native dialog with a visually positioned div. No production dialog flow is introduced by this foundation issue.

Layouts start at 320px, remain single-column on phones, and may use two columns from 48rem (tablet). Use 64rem for additional desktop space only where needed; the existing 24rem rule handles very narrow invitation rows. CSS custom properties cannot be substituted into media-query conditions, so keep these documented values literal. Test at 360, 768, and 1280px with long names, zoom/reflow, and keyboard navigation.

Use at least 44px interaction targets (shared controls default to 48px). Semantic text pairs target WCAG AA 4.5:1 and control boundaries/focus indicators 3:1. Disabled controls are visibly inactive rather than a substitute for explaining unavailable actions. Honor `prefers-reduced-motion`: remove decorative animation/transitions, keep visible loading/status text, and never gate actions on animation completion. Transitions use 140–220ms with the shared easing; avoid flashing or screen-wide motion.

Icons use inline SVG, a 24-unit view box, 2-unit rounded strokes, `currentColor`, and `.wg-icon`; decorative icons use `aria-hidden`, while icon-only controls require a name. Keep icons paired with text for important game actions. The homepage arrow is the initial reference.

Browser checks cover responsive layouts, contrast pairs, keyboard focus, native dialog dismissal, touch size, and reduced motion. Component tests cover busy-action guarding and labelled error descriptions. Later UI issues can extend screens using this foundation.

Gameplay feedback follows authoritative state: phase/round entry, bids, bluff calls, word results, changed scores, and final winners use short opacity/transform effects. The challenge countdown briefly emphasizes each of its final ten seconds; the warning color and underline remain without motion. Winner stars animate once for 440ms; other effects take 140–220ms. Existing loading and matchmaking indicators are reused. No action or server timer waits for an animation, and reduced motion removes all effects while preserving text/status feedback. Mobile browser checks include reduced motion and Chromium CPU throttling; physical-device smoothness remains part of release validation.

## Mobile viewport and packaging

The viewport permits zoom, opts into safe-area layout, and requests content resizing for interactive widgets where supported. Body padding consumes `safe-area-inset-*`; inputs use at least 16px text. Pages remain naturally scrollable without fixed-screen locks. Challenge entry observes [VisualViewport](https://developer.mozilla.org/en-US/docs/Web/API/VisualViewport) resize/focus events and, in a short visible viewport, brings the timer, progress, field, and submit action into a compact sticky group. Word entry disables autocorrect/capitalization and supplies a Send key hint; Enter uses the same form submission. Zoom/orientation changes do not reset game state. See [viewport metadata](https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Elements/meta/name/viewport) for browser-dependent widget behavior.

Chromium and iPhone-emulated WebKit automation exercise real gameplay; keyboard tests explicitly contract the viewport because automation does not open native OS keyboards. This is engine/emulation coverage, not physical iOS Safari/Android Chrome certification. Before release or app packaging, check native keyboard/IME behavior, real browser zoom, notch insets, rotation, background/resume SSE recovery, and cookie persistence on representative devices/WKWebView/Android WebView. No native wrapper or orientation lock is introduced here. Prefer HTTPS/HTTP2 for deployment because [HTTP/1.1 SSE connection limits](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events/Using_server-sent_events) can stall multiple tabs; multi-player WebKit fixtures use separate browser processes to represent independent devices.
