import type { Lobby } from '../../src/services/api/lobbies.ts'

// Shared test state; no production feature imports this fixture.
export const lobbyFixture: Lobby = {
  id: 'eea0b4cf-a3c7-4334-9af6-26dd94833d91',
  lobbyCode: 'ABC234',
  hostPlayerId: 'host',
  status: 'waiting',
  players: [
    { id: 'host', displayName: 'Host', isReady: false, isConnected: true },
    { id: 'bob', displayName: 'Bob', isReady: false, isConnected: true },
  ],
  minimumPlayers: 2,
  maximumPlayers: 4,
  createdAt: '2026-11-19T12:00:00Z',
  expiresAt: '2026-11-19T12:30:00Z',
  version: '1',
  gameId: null,
  canStart: false,
  settings: {
    maximumPlayers: 4,
    roundCount: null,
    challengeSeconds: 30,
    allowedDifficulties: ['easy', 'medium', 'hard'],
  },
}
