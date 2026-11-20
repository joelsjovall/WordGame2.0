import type { MatchmakingState } from '../../src/services/api/matchmaking.ts'
import { gameFixture } from './game.ts'

export const idleMatchmaking: MatchmakingState = {
  searchId: null,
  status: 'idle',
  createdAt: null,
  expiresAt: null,
  matchId: null,
  gameId: null,
  hasAccepted: false,
}
export const searchingMatchmaking: MatchmakingState = {
  ...idleMatchmaking,
  searchId: 'search-a',
  status: 'searching',
  createdAt: new Date().toISOString(),
  expiresAt: new Date(Date.now() + 120000).toISOString(),
}
export const foundMatchmaking: MatchmakingState = {
  ...searchingMatchmaking,
  status: 'matched',
  matchId: 'match-a',
  gameId: gameFixture.id,
  expiresAt: new Date(Date.now() + 30000).toISOString(),
}
