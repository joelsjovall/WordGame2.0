// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import { GameView } from './GameView.tsx'
import * as gameHook from './useGameState.ts'
import { gamesApi } from '../../services/api/games.ts'
import { gameSynchronization } from '../../services/gameSynchronization.ts'
import { gameFixture } from '../../../tests/fixtures/game.ts'

const online = {
  ...gameFixture,
  match: { ...gameFixture.match, format: 'online' as const },
}
test('tied Online results clearly announce a shared victory', async () => {
  const { OnlineOutcome } = await import('./OnlineOutcome.tsx')
  render(
    <OnlineOutcome
      playerId="bob"
      state={{
        ...online,
        status: 'completed',
        result: {
          gameId: online.id,
          format: 'online',
          completedRoundCount: 6,
          isTie: true,
          winnerIds: ['bob', 'host'],
          reason: 'completed',
          forfeitedPlayerId: null,
          standings: [
            { playerId: 'bob', displayName: 'Bob', score: 60, rank: 1 },
            { playerId: 'host', displayName: 'Host', score: 60, rank: 1 },
          ],
        },
      }}
    />,
  )
  expect(screen.getByRole('heading', { name: 'Shared victory' })).toBeVisible()
  expect(screen.getByLabelText('Final standings')).toHaveTextContent(
    'Bob: 60 points',
  )
})
afterEach(() => vi.restoreAllMocks())

test('Online uses the explicit forfeit command and disables it while recovering', async () => {
  const hook = vi.spyOn(gameHook, 'useGameState').mockReturnValue({
    state: online,
    connection: 'connected',
    error: null,
    receivedAt: 0,
  })
  const action = vi
    .spyOn(gamesApi, 'act')
    .mockResolvedValue({ actionId: 'action', version: '6' })
  vi.spyOn(gameSynchronization, 'refresh').mockResolvedValue()
  const view = render(
    <MemoryRouter>
      <GameView id={online.id} playerId="bob" />
    </MemoryRouter>,
  )
  expect(
    screen.queryByRole('button', { name: 'Leave match' }),
  ).not.toBeInTheDocument()
  await userEvent
    .setup()
    .click(screen.getByRole('button', { name: 'Forfeit Match' }))
  expect(action).toHaveBeenCalledWith(
    online.id,
    expect.objectContaining({
      type: 'forfeitMatch',
      expectedVersion: online.version,
    }),
  )
  hook.mockReturnValue({
    state: online,
    connection: 'recovering',
    error: null,
    receivedAt: 0,
  })
  view.rerender(
    <MemoryRouter>
      <GameView id={online.id} playerId="bob" />
    </MemoryRouter>,
  )
  expect(screen.getByRole('button', { name: 'Forfeit Match' })).toBeDisabled()
})

test.each(['forfeit', 'reconnectionExpired'] as const)(
  'the server-authoritative %s result is shown without waiting for a lobby',
  (reason) => {
    const state = {
      ...online,
      status: 'completed' as const,
      result: {
        gameId: online.id,
        format: 'online' as const,
        completedRoundCount: 0,
        isTie: false,
        winnerIds: ['bob'],
        reason,
        forfeitedPlayerId: 'host',
        standings: [
          { playerId: 'bob', displayName: 'Bob', score: 0, rank: 1 },
          { playerId: 'host', displayName: 'Host', score: 50, rank: 2 },
        ],
      },
    }
    vi.spyOn(gameHook, 'useGameState').mockReturnValue({
      state,
      connection: 'connected',
      error: null,
      receivedAt: 0,
    })
    render(
      <MemoryRouter>
        <GameView id={state.id} playerId="bob" />
      </MemoryRouter>,
    )
    expect(screen.getByRole('heading', { name: 'You won' })).toBeVisible()
    expect(
      screen.getByText(
        reason === 'forfeit'
          ? 'Host forfeited the match.'
          : 'Host did not reconnect in time.',
      ),
    ).toBeVisible()
    expect(screen.getByLabelText('Final standings')).toHaveTextContent(
      'Host: 50 points (rank 2)',
    )
    expect(
      screen.queryByRole('button', { name: 'Forfeit Match' }),
    ).not.toBeInTheDocument()
  },
)

test('an abandoned Online match shows no winner rather than promising Party results', () => {
  vi.spyOn(gameHook, 'useGameState').mockReturnValue({
    state: { ...online, status: 'abandoned' },
    connection: 'connected',
    error: null,
    receivedAt: 0,
  })
  render(
    <MemoryRouter>
      <GameView id={online.id} playerId="bob" />
    </MemoryRouter>,
  )
  expect(screen.getByText('The match ended without a winner.')).toBeVisible()
  expect(
    screen.queryByRole('button', { name: 'Forfeit Match' }),
  ).not.toBeInTheDocument()
})
