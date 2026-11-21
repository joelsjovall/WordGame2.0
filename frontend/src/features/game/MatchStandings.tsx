import type { GameState } from '../../services/api/games.ts'

export function MatchStandings({
  standings,
}: {
  standings: NonNullable<GameState['result']>['standings']
}) {
  return (
    <ol className="game-standings" aria-label="Final standings">
      {standings.map((standing) => (
        <li key={standing.playerId} data-winner={standing.rank === 1}>
          <span className="game-rank" aria-hidden="true">
            {standing.rank}
          </span>
          <span>
            {standing.displayName}: <strong>{standing.score} points</strong>{' '}
            (rank {standing.rank})
          </span>
        </li>
      ))}
    </ol>
  )
}
