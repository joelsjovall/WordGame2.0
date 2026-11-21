import { useEffect, useRef, type FocusEvent } from 'react'
import type { GameState } from '../../services/api/games.ts'

// A server transition can remove the focused action. Recover only that lost
// focus, leaving word entry, dialogs and navigation controls undisturbed.
export function useGameFocus(state: GameState | null) {
  const root = useRef<HTMLDivElement>(null)
  const wasInStage = useRef(false)
  useEffect(() => {
    if (wasInStage.current && document.activeElement === document.body) {
      const heading = root.current?.querySelector<HTMLElement>(
        '.game-stage h2, .game-results h2',
      )
      if (heading) {
        heading.tabIndex = -1
        heading.focus({ preventScroll: true })
      }
    }
  }, [
    state?.round?.id,
    state?.round?.status,
    state?.round?.highestBid?.wordCount,
    state?.round?.activePlayerId,
    state?.status,
  ])
  function onFocusCapture(event: FocusEvent<HTMLDivElement>) {
    wasInStage.current = Boolean(
      (event.target as HTMLElement).closest('.game-stage'),
    )
  }
  return { ref: root, onFocusCapture }
}
