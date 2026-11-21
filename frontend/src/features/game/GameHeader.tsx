import { Badge } from '../../components/ui.tsx'
import type { GameState } from '../../services/api/games.ts'

const phases = {
  categorySelection: 'Choose a category',
  bidding: 'Bidding',
  challenge: 'Word challenge',
  completed: 'Round complete',
  cancelled: 'Round cancelled',
}
export function GameHeader({ state }: { state: GameState }) {
  const phase =
    state.status === 'completed'
      ? 'Match complete'
      : state.status === 'abandoned'
        ? 'Match ended'
        : state.round
          ? phases[state.round.status]
          : 'Preparing match'
  return (
    <header className="game-header">
      <div>
        <p className="eyebrow">
          {state.match.format === 'online'
            ? 'Online Mode · 1 vs 1'
            : 'Party Mode'}
        </p>
        <h1>
          {state.status === 'completed'
            ? 'Match complete'
            : state.status === 'abandoned'
              ? 'Match ended'
              : 'Match started'}
        </h1>
        <p>
          Round {state.round?.number ?? 0} of {state.match.roundCount}
        </p>
      </div>
      <div className="game-phase">
        <Badge>{phase}</Badge>
        {state.round?.category && (
          <p>
            Category: <strong>{state.round.category.name}</strong>{' '}
            <Badge tone="warning">{state.round.category.difficulty}</Badge>
          </p>
        )}
      </div>
      <p
        className="sr-only"
        role="status"
        aria-label="Game turn"
        aria-atomic="true"
      >
        Round {state.round?.number ?? 0}. {phase}.
        {state.status === 'active' &&
          state.round?.activePlayerId &&
          state.round.status !== 'completed' &&
          state.round.status !== 'cancelled' &&
          ` ${state.players.find((player) => player.id === state.round?.activePlayerId)?.displayName ?? 'Another player'} is playing.`}
      </p>
    </header>
  )
}
