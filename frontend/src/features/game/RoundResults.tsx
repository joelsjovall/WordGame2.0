import { Badge, Card } from '../../components/ui.tsx'
import type { GameState } from '../../services/api/games.ts'

export function RoundResults({ state }: { state: GameState }) {
  const result = state.round?.result
  const winner = state.players.find(
    (player) => player.id === result?.winnerId,
  )?.displayName
  return (
    <Card className="game-panel game-round-results">
      <Badge tone="success">Points awarded</Badge>
      <h2>Round complete</h2>
      {result && (
        <p>
          {winner} wins the round. {result.acceptedWordCount} of{' '}
          {result.requiredWordCount} words accepted.
        </p>
      )}
      <p>Next round starts automatically.</p>
      {result && (
        <ul className="game-round-scores" aria-label="Round scores">
          {result.scores.map((score) => (
            <li key={score.playerId}>
              {
                state.players.find((player) => player.id === score.playerId)
                  ?.displayName
              }
              : <strong>+{score.pointsAwarded}</strong>
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}
