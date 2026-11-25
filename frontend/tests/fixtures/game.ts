import type { GameState } from '../../src/services/api/games.ts'

export const gameFixture: GameState = {
  id: 'd9ae6b2c-8104-4720-9e56-a2a043fcd119',
  version: '5',
  status: 'active',
  serverTime: '2026-11-19T12:00:00Z',
  players: [
    {
      id: 'host',
      displayName: 'Host',
      score: 0,
      isConnected: true,
      hasLeft: false,
    },
    {
      id: 'bob',
      displayName: 'Bob',
      score: 0,
      isConnected: true,
      hasLeft: false,
    },
  ],
  match: {
    format: 'party',
    roundCount: 1,
    challengeSeconds: 5,
    allowedDifficulties: ['easy'],
    sealedOpening: false,
  },
  result: null,
  round: {
    id: 'round',
    number: 1,
    status: 'bidding',
    activePlayerId: 'bob',
    category: { id: 'capitals', name: 'Capitals', difficulty: 'easy' },
    highestBid: { playerId: 'host', wordCount: 1, risk: false },
    challenge: null,
    result: null,
    categorySelectionDeadline: null,
    sealedOpeningPending: false,
  },
}
