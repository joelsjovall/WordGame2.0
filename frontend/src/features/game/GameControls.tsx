import { useState, type FormEvent } from 'react'
import type { GameAction, GameState } from '../../services/api/games.ts'
import { CategoryChoice } from './CategoryChoice.tsx'
import { ServerCountdown } from './ServerCountdown.tsx'

type Command = GameAction extends infer Action
  ? Action extends GameAction
    ? Omit<Action, 'actionId' | 'expectedVersion'>
    : never
  : never
export function GameControls({
  state,
  playerId,
  busy,
  send,
}: {
  state: GameState
  playerId: string
  busy: boolean
  send: (command: Command) => void
}) {
  const round = state.round
  const [bid, setBid] = useState((round?.highestBid?.wordCount ?? 0) + 1)
  const [word, setWord] = useState('')
  if (!round || state.status !== 'active') return null
  const turn = round.activePlayerId === playerId
  const activeName =
    state.players.find((player) => player.id === round.activePlayerId)
      ?.displayName ?? 'Another player'
  function submitWord(event: FormEvent) {
    event.preventDefault()
    send({ type: 'submitWord', word })
    setWord('')
  }
  if (round.status === 'categorySelection')
    return turn ? (
      <CategoryChoice
        allowed={state.match.allowedDifficulties}
        busy={busy}
        choose={(categoryId) => send({ type: 'selectCategory', categoryId })}
      />
    ) : (
      <p>{activeName} is selecting a category.</p>
    )
  if (round.status === 'bidding')
    return (
      <section className="lobby-card">
        <h2>Bidding</h2>
        <p>
          {round.highestBid
            ? `Highest bid: ${round.highestBid.wordCount} words`
            : 'No bid yet.'}
        </p>
        {turn ? (
          <>
            <form
              onSubmit={(event) => {
                event.preventDefault()
                send({ type: 'placeBid', wordCount: bid })
              }}
            >
              <label htmlFor="word-bid">Your bid</label>
              <input
                id="word-bid"
                type="number"
                min={(round.highestBid?.wordCount ?? 0) + 1}
                required
                value={bid}
                onChange={(event) => setBid(Number(event.target.value))}
              />
              <button disabled={busy}>Place bid</button>
            </form>
            {round.highestBid && round.highestBid.playerId !== playerId && (
              <button
                disabled={busy}
                onClick={() => send({ type: 'callBluff' })}
              >
                Call bluff
              </button>
            )}
          </>
        ) : (
          <p>{activeName} is bidding.</p>
        )}
      </section>
    )
  if (round.status === 'challenge' && round.challenge)
    return (
      <section className="lobby-card">
        <h2>Word challenge</h2>
        <p>
          Time remaining:{' '}
          <ServerCountdown
            key={round.challenge.id}
            deadline={round.challenge.deadline}
            serverTime={state.serverTime}
          />{' '}
          seconds
        </p>
        <p>
          {round.challenge.acceptedWordCount} of{' '}
          {round.challenge.requiredWordCount} words accepted
        </p>
        <ul aria-label="Submitted words">
          {round.challenge.submissions.map((submission, index) => (
            <li key={index}>
              {submission.text}: {submission.status}
            </li>
          ))}
        </ul>
        {round.challenge.playerId === playerId ? (
          <form onSubmit={submitWord}>
            <label htmlFor="challenge-word">Your word</label>
            <input
              id="challenge-word"
              required
              autoComplete="off"
              value={word}
              onChange={(event) => setWord(event.target.value)}
            />
            <button disabled={busy}>Submit word</button>
          </form>
        ) : (
          <p>{activeName} is naming words.</p>
        )}
      </section>
    )
  if (round.status === 'completed')
    return (
      <section>
        <h2>Round complete</h2>
        <p>Next round starts automatically.</p>
        {round.result && (
          <ul aria-label="Round scores">
            {round.result.scores.map((score) => (
              <li key={score.playerId}>
                {
                  state.players.find((player) => player.id === score.playerId)
                    ?.displayName
                }
                : +{score.pointsAwarded}
              </li>
            ))}
          </ul>
        )}
      </section>
    )
  return null
}
export type GameCommand = Command
