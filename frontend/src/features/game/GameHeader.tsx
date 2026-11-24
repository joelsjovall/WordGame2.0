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
  const steps = [
    { status: 'categorySelection', label: 'Choose category' },
    { status: 'bidding', label: 'Bid or call' },
    { status: 'challenge', label: 'Name words' },
    { status: 'completed', label: 'Round results' },
  ] as const
  const currentStep = steps.findIndex(
    (step) => step.status === state.round?.status,
  )
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
      {state.status === 'active' && state.round && currentStep >= 0 && (
        <ol className="game-stepper" aria-label="Round progress">
          {steps.map((step, index) => (
            <li
              key={step.status}
              data-state={
                index < currentStep
                  ? 'done'
                  : index === currentStep
                    ? 'current'
                    : 'upcoming'
              }
              aria-current={index === currentStep ? 'step' : undefined}
            >
              <span className="game-step-number" aria-hidden="true">
                {index + 1}
              </span>
              <span>{step.label}</span>
            </li>
          ))}
        </ol>
      )}
    </header>
  )
}
