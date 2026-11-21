import type { GameState } from '../../services/api/games.ts'
import { MatchStandings } from './MatchStandings.tsx'
import { Badge, Card } from '../../components/ui.tsx'

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
  const won = result.winnerIds.includes(playerId)
  return (
    <Card className="game-results game-panel" aria-label="Online match outcome">
      <h2 aria-live="polite" aria-atomic="true">
        {result.isTie ? 'Shared victory' : won ? 'You won' : 'Match over'}
      </h2>
      {result.reason === 'forfeit' && <p>{departed} forfeited the match.</p>}
      {result.reason === 'reconnectionExpired' && (
        <p>{departed} did not reconnect in time.</p>
      )}
      {result.isTie && <Badge tone="warning">Tied top score</Badge>}
      <MatchStandings standings={result.standings} />
      <p>The match has ended. Your final scores are preserved.</p>
    </Card>
  )
}
