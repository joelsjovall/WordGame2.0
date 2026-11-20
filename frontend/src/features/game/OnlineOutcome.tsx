import type { GameState } from '../../services/api/games.ts'

export function OnlineOutcome({
  state,
  playerId,
}: {
  state: GameState
  playerId: string
}) {
  const result = state.result
  if (!result) return null
  const departed =
    state.players.find((player) => player.id === result.forfeitedPlayerId)
      ?.displayName ?? 'Your opponent'
  return (
    <section aria-label="Online match outcome">
      <h2>{result.winnerIds.includes(playerId) ? 'You won' : 'Match over'}</h2>
      {result.reason === 'forfeit' && <p>{departed} forfeited the match.</p>}
      {result.reason === 'reconnectionExpired' && (
        <p>{departed} did not reconnect in time.</p>
      )}
      <ol aria-label="Final standings">
        {result.standings.map((standing) => (
          <li key={standing.playerId}>
            {standing.displayName}: {standing.score} points (rank{' '}
            {standing.rank})
          </li>
        ))}
      </ol>
      <p>The match has ended. Your final scores are preserved.</p>
    </section>
  )
}
