import { useRef, useState } from 'react'
import { Badge, Button, Card, TextField } from '../../components/ui.tsx'
import { ServerCountdown } from './ServerCountdown.tsx'
import type { GamePanelProps } from './gameTypes.ts'
import { useCompactEntry } from './useCompactEntry.ts'

export function ChallengePanel({
  state,
  playerId,
  busy,
  send,
}: GamePanelProps) {
  const challenge = state.round!.challenge!
  const [word, setWord] = useState('')
  const entry = useRef<HTMLDivElement>(null)
  const compact = useCompactEntry(entry)
  const canSubmit =
    challenge.playerId === playerId &&
    state.round?.activePlayerId === playerId &&
    challenge.outcome === 'pending'
  const bidder =
    state.players.find((player) => player.id === challenge.playerId)
      ?.displayName ?? 'Another player'
  return (
    <Card className="game-panel" aria-labelledby="challenge-title">
      <p className="eyebrow game-bluff-called">
        Bluff called · Time to prove it
      </p>
      <h2 id="challenge-title">Word challenge</h2>
      <div ref={entry} className="game-challenge-entry" data-compact={compact}>
        <div className="game-challenge-summary">
          <div
            className="game-timer"
            role="timer"
            aria-label="Challenge time remaining"
          >
            Time remaining:{' '}
            <strong>
              <ServerCountdown
                key={challenge.id}
                deadline={challenge.deadline}
                serverTime={state.serverTime}
                showUrgency
                soundId={challenge.id}
              />
            </strong>{' '}
            seconds
          </div>
          <p>
            {challenge.acceptedWordCount} of {challenge.requiredWordCount} words
            accepted
          </p>
        </div>
        <progress
          aria-label="Accepted word progress"
          max={challenge.requiredWordCount}
          value={challenge.acceptedWordCount}
        />
        {canSubmit ? (
          <form
            onSubmit={(event) => {
              event.preventDefault()
              if (!busy && word.trim() && word.length <= 256) {
                send({ type: 'submitWord', word })
                setWord('')
              }
            }}
          >
            <TextField
              label="Your word"
              id="challenge-word"
              required
              maxLength={256}
              autoComplete="off"
              autoCapitalize="none"
              autoCorrect="off"
              enterKeyHint="send"
              spellCheck={false}
              aria-describedby="challenge-word-help"
              value={word}
              onChange={(event) => setWord(event.target.value)}
            />
            <p id="challenge-word-help" className="sr-only">
              Submit one answer at a time. Each different accepted answer counts
              once.
            </p>
            <Button type="submit" busy={busy} disabled={busy || !word.trim()}>
              Submit word
            </Button>
          </form>
        ) : (
          <p role="status">{bidder} is naming words.</p>
        )}
      </div>
      <h3>Words on the table</h3>
      {challenge.submissions.length === 0 && (
        <p className="game-hint">No words submitted yet.</p>
      )}
      <ul className="game-words" aria-label="Submitted words">
        {challenge.submissions.map((submission, index) => (
          <li key={index} data-status={submission.status}>
            <span>{submission.text}</span>
            <Badge
              tone={submission.status === 'accepted' ? 'success' : 'error'}
            >
              {submission.status}
            </Badge>
          </li>
        ))}
      </ul>
      <p
        className="sr-only"
        role="status"
        aria-label="Word submission feedback"
        aria-atomic="true"
      >
        {challenge.submissions.length > 0 &&
          `Answer ${challenge.submissions.length}: ${challenge.submissions.at(-1)!.text}, ${challenge.submissions.at(-1)!.status}. ${challenge.acceptedWordCount} of ${challenge.requiredWordCount} accepted.`}
      </p>
    </Card>
  )
}
