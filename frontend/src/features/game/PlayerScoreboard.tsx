import { Badge } from '../../components/ui.tsx'
import type { GameState } from '../../services/api/games.ts'
import { ServerCountdown } from './ServerCountdown.tsx'

export function PlayerScoreboard({
  state,
  playerId,
}: {
  state: GameState
  playerId: string
}) {
  return (
    <ul className="game-scoreboard" aria-label="Match scores">
      {state.players.map((player) => {
        const active =
          state.status === 'active' &&
          state.round?.status !== 'completed' &&
          state.round?.status !== 'cancelled' &&
          state.round?.activePlayerId === player.id &&
          !player.hasLeft
        return (
          <li key={player.id} data-active={active}>
            <span className="game-avatar" aria-hidden="true">
              {Array.from(player.displayName)[0]?.toUpperCase()}
            </span>
            <div>
              <p>
                <strong>{player.displayName}</strong>:{' '}
                <span key={player.score} className="game-score">
                  {player.score} points
                </span>
              </p>
              <p className="game-presence">
                {player.hasLeft ? (
                  'Left match'
                ) : player.isConnected ? (
                  'Connected'
                ) : player.reconnectUntil ? (
                  <>
                    Reconnecting, grace ends in{' '}
                    <ServerCountdown
                      key={player.reconnectUntil}
                      deadline={player.reconnectUntil}
                      serverTime={state.serverTime}
                    />{' '}
                    seconds
                  </>
                ) : (
                  'Offline'
                )}
              </p>
              {active ? (
                <Badge tone="success">
                  Active turn{player.id === playerId ? ' · You' : ''}
                </Badge>
              ) : (
                player.id === playerId && <span className="game-you">You</span>
              )}
            </div>
          </li>
        )
      })}
    </ul>
  )
}
