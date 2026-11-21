import { useState } from 'react'
import { Badge, Button, Card, TextField } from '../../components/ui.tsx'
import { ServerCountdown } from './ServerCountdown.tsx'
import type { GamePanelProps } from './gameTypes.ts'

export function ChallengePanel({
  state,
  playerId,
  busy,
  send,
}: GamePanelProps) {
  const challenge = state.round!.challenge!
  const [word, setWord] = useState('')
  const canSubmit =
    challenge.playerId === playerId &&
    state.round?.activePlayerId === playerId &&
    challenge.outcome === 'pending'
  const bidder =
    state.players.find((player) => player.id === challenge.playerId)
      ?.displayName ?? 'Another player'
  return (
    <Card className="game-panel" aria-labelledby="challenge-title">
      <p className="eyebrow">Time to prove it</p>
      <h2 id="challenge-title">Word challenge</h2>
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
            spellCheck={false}
            disabled={busy}
            value={word}
            onChange={(event) => setWord(event.target.value)}
          />
          <Button type="submit" disabled={busy || !word.trim()}>
            Submit word
          </Button>
        </form>
      ) : (
        <p role="status">{bidder} is naming words.</p>
      )}
      <h3>Words on the table</h3>
      {challenge.submissions.length === 0 && (
        <p className="game-hint">No words submitted yet.</p>
      )}
      <ul
        className="game-words"
        aria-label="Submitted words"
        aria-live="polite"
      >
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
    </Card>
  )
}
