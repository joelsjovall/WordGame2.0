import { useState } from 'react'
import { Button } from '../../components/ui.tsx'
import type { GameState } from '../../services/api/games.ts'

export function ShareMatchResult({
  standings,
}: {
  standings: NonNullable<GameState['result']>['standings']
}) {
  const [message, setMessage] = useState('')
  const text = `WordGame results\n${standings
    .map(
      (player) =>
        `${player.rank}. ${player.displayName}: ${player.score} points`,
    )
    .join('\n')}`

  async function share() {
    try {
      if (navigator.share) {
        await navigator.share({ title: 'WordGame results', text })
        setMessage('Result shared.')
      } else if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(text)
        setMessage('Result copied.')
      } else {
        setMessage('Copy the result below to share it.')
      }
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      setMessage('Sharing failed. Copy the result below instead.')
    }
  }

  return (
    <div className="game-share-result">
      <Button variant="secondary" onClick={() => void share()}>
        Share results
      </Button>
      {message && <p role="status">{message}</p>}
      {message.includes('below') && (
        <textarea aria-label="Result to copy" readOnly value={text} />
      )}
    </div>
  )
}
