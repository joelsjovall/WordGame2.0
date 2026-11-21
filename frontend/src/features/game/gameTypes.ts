import type { GameAction, GameState } from '../../services/api/games.ts'

export type GameCommand = GameAction extends infer Action
  ? Action extends GameAction
    ? Omit<Action, 'actionId' | 'expectedVersion'>
    : never
  : never
export type GamePanelProps = {
  state: GameState
  playerId: string
  busy: boolean
  send: (command: GameCommand) => void
}
