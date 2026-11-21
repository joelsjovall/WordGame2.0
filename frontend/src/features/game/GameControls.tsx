import { CategoryChoice } from './CategoryChoice.tsx'
import { Card } from '../../components/ui.tsx'
import { BiddingPanel } from './BiddingPanel.tsx'
import { ChallengePanel } from './ChallengePanel.tsx'
import { RoundResults } from './RoundResults.tsx'
import type { GamePanelProps } from './gameTypes.ts'
export type { GameCommand } from './gameTypes.ts'

export function GameControls(props: GamePanelProps) {
  const { state, playerId, busy, send } = props
  const round = state.round
  if (!round || state.status !== 'active') return null
  if (round.status === 'categorySelection') {
    if (round.activePlayerId === playerId)
      return (
        <CategoryChoice
          allowed={state.match.allowedDifficulties}
          busy={busy}
          choose={(categoryId) => send({ type: 'selectCategory', categoryId })}
        />
      )
    const name =
      state.players.find((player) => player.id === round.activePlayerId)
        ?.displayName ?? 'Another player'
    return (
      <Card className="game-panel">
        <h2>Category selection</h2>
        <p role="status">{name} is selecting a category.</p>
      </Card>
    )
  }
  if (round.status === 'bidding') return <BiddingPanel {...props} />
  if (round.status === 'challenge' && round.challenge)
    return <ChallengePanel {...props} />
  if (round.status === 'completed') return <RoundResults state={state} />
  return null
}
