// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import * as stateHook from './useGameState.ts'
import { PartyGame } from './PartyGame.tsx'
import { gamesApi } from '../../services/api/games.ts'
import { gameSynchronization } from '../../services/gameSynchronization.ts'
import { gameFixture as game } from '../../../tests/fixtures/game.ts'

afterEach(() => vi.restoreAllMocks())
test('recovery preserves the visible score but disables gameplay and intentional leave until connected', async () => {
  const hook = vi.spyOn(stateHook, 'useGameState').mockReturnValue({
    state: game,
    connection: 'recovering',
    error: null,
    receivedAt: 0,
  })
  const view = render(
    <MemoryRouter>
      <PartyGame id={game.id} playerId="bob" />
    </MemoryRouter>,
  )
  expect(screen.getByRole('button', { name: 'Place bid' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Leave match' })).toBeDisabled()
  expect(screen.getByLabelText('Match scores')).toHaveTextContent(
    'Host: 0 points',
  )
  expect(screen.getByRole('status')).toHaveTextContent(
    'Timers continue on the server.',
  )
  hook.mockReturnValue({
    state: game,
    connection: 'connected',
    error: null,
    receivedAt: 0,
  })
  const action = vi
    .spyOn(gamesApi, 'act')
    .mockResolvedValue({ actionId: 'action', version: '6' })
  vi.spyOn(gameSynchronization, 'refresh').mockResolvedValue(undefined)
  view.rerender(
    <MemoryRouter>
      <PartyGame id={game.id} playerId="bob" />
    </MemoryRouter>,
  )
  await userEvent
    .setup()
    .click(screen.getByRole('button', { name: 'Leave match' }))
  expect(action).toHaveBeenCalledWith(game.id, {
    type: 'leaveMatch',
    expectedVersion: game.version,
    actionId: expect.any(String),
  })
})
test('terminal sessions offer recovery guidance without loading forever and departed players get no controls', () => {
  const hook = vi.spyOn(stateHook, 'useGameState').mockReturnValue({
    state: null,
    connection: 'error',
    error: null,
    receivedAt: 0,
  })
  const view = render(
    <MemoryRouter>
      <PartyGame id={game.id} playerId="bob" />
    </MemoryRouter>,
  )
  expect(screen.getByRole('link', { name: 'Back to homepage' })).toBeVisible()
  expect(screen.queryByText('Loading game…')).not.toBeInTheDocument()
  hook.mockReturnValue({
    state: {
      ...game,
      players: game.players.map((player) => ({
        ...player,
        hasLeft: player.id === 'bob',
      })),
    },
    connection: 'connected',
    error: null,
    receivedAt: 0,
  })
  view.rerender(
    <MemoryRouter>
      <PartyGame id={game.id} playerId="bob" />
    </MemoryRouter>,
  )
  expect(
    screen.queryByRole('button', { name: 'Place bid' }),
  ).not.toBeInTheDocument()
  expect(
    screen.queryByRole('button', { name: 'Leave match' }),
  ).not.toBeInTheDocument()
  expect(screen.getByText(/Reopening this page will not rejoin/)).toBeVisible()
})
