// @vitest-environment jsdom
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import { GameControls } from './GameControls.tsx'
import { GameView } from './GameView.tsx'
import * as stateHook from './useGameState.ts'
import { gamesApi } from '../../services/api/games.ts'
import { ApiError } from '../../services/api/client.ts'
import { gameSynchronization } from '../../services/gameSynchronization.ts'
import { gameFixture as game } from '../../../tests/fixtures/game.ts'
import type { GameState } from '../../services/api/games.ts'
import { PlayerScoreboard } from './PlayerScoreboard.tsx'
import { ServerCountdown } from './ServerCountdown.tsx'
import { gameAudio } from '../../services/gameAudio.ts'

afterEach(() => {
  vi.restoreAllMocks()
  vi.useRealTimers()
})

test('score emphasis restarts only for changed scores, not presence updates', () => {
  const view = render(<PlayerScoreboard state={game} playerId="host" />)
  const scores = screen.getAllByText('0 points')
  view.rerender(
    <PlayerScoreboard
      state={{
        ...game,
        players: game.players.map((player) => ({
          ...player,
          isConnected: false,
        })),
      }}
      playerId="host"
    />,
  )
  expect(screen.getAllByText('0 points')[0]).toBe(scores[0])
  view.rerender(
    <PlayerScoreboard
      state={{
        ...game,
        players: game.players.map((player) =>
          player.id === 'host' ? { ...player, score: 10 } : player,
        ),
      }}
      playerId="host"
    />,
  )
  expect(screen.getByText('10 points')).not.toBe(scores[0])
  expect(screen.getByText('0 points')).toBe(scores[1])
})

test('urgency uses the server deadline and stops at zero without triggering actions', () => {
  const play = vi.spyOn(gameAudio, 'play').mockImplementation(() => {})
  vi.useFakeTimers()
  const now = vi.spyOn(performance, 'now').mockReturnValue(0)
  const view = render(
    <ServerCountdown
      deadline="2026-11-19T12:00:11Z"
      serverTime={game.serverTime}
      showUrgency
      soundId="challenge-warning"
    />,
  )
  expect(view.container.firstChild).toHaveAttribute('data-urgent', 'false')
  now.mockReturnValue(1000)
  act(() => vi.advanceTimersByTime(1000))
  expect(view.container.firstChild).toHaveAttribute('data-urgent', 'true')
  expect(screen.getByText('10')).toBeVisible()
  expect(play).not.toHaveBeenCalled()
  now.mockReturnValue(6000)
  act(() => vi.advanceTimersByTime(5000))
  expect(play).toHaveBeenCalledExactlyOnceWith(
    'challenge-warning/warning',
    'warning',
  )
  now.mockReturnValue(12000)
  act(() => vi.advanceTimersByTime(6000))
  expect(screen.getByText('0')).toBeVisible()
  expect(view.container.firstChild).toHaveAttribute('data-urgent', 'false')
  expect(play).toHaveBeenCalledTimes(1)
})
test('invalid bids cannot be sent but calling an opponent bluff stays available', async () => {
  const send = vi.fn()
  render(<GameControls state={game} playerId="bob" busy={false} send={send} />)
  const input = screen.getByLabelText('Your bid')
  await userEvent.setup().clear(input)
  await userEvent.setup().type(input, '1')
  expect(screen.getByRole('button', { name: 'Place bid' })).toBeDisabled()
  await userEvent
    .setup()
    .click(screen.getByRole('button', { name: 'Call bluff' }))
  expect(send).toHaveBeenCalledExactlyOnceWith({ type: 'callBluff' })
})
test('challenge shows authoritative accepted/rejected words, progress and countdown', async () => {
  const state: GameState = {
    ...game,
    round: {
      ...game.round!,
      status: 'challenge',
      activePlayerId: 'host',
      challenge: {
        id: 'challenge',
        playerId: 'host',
        callerId: 'bob',
        requiredWordCount: 3,
        acceptedWordCount: 1,
        startedAt: game.serverTime,
        deadline: '2026-11-19T12:00:05Z',
        outcome: 'pending',
        submissions: [
          {
            text: 'Stockholm',
            status: 'accepted',
          },
          {
            text: 'Nope',
            status: 'rejected',
          },
        ],
      },
    },
  }
  const view = render(
    <GameControls state={state} playerId="host" busy={false} send={vi.fn()} />,
  )
  expect(screen.getByRole('timer')).toHaveTextContent('5')
  expect(screen.getByRole('progressbar')).toHaveAttribute('value', '1')
  expect(screen.getByText('1 of 3 words accepted')).toBeVisible()
  expect(
    within(screen.getByLabelText('Submitted words')).getByText('rejected'),
  ).toBeVisible()
  expect(screen.getByRole('button', { name: 'Submit word' })).toBeDisabled()
  await userEvent.setup().type(screen.getByLabelText('Your word'), '   ')
  expect(screen.getByRole('button', { name: 'Submit word' })).toBeDisabled()
  expect(screen.getByLabelText('Your word')).toHaveAttribute('maxlength', '256')
  view.rerender(
    <GameControls state={state} playerId="host" busy send={vi.fn()} />,
  )
  expect(screen.getByLabelText('Your word')).toHaveFocus()
  expect(screen.getByLabelText('Your word')).toBeEnabled()
  await userEvent.setup().clear(screen.getByLabelText('Your word'))
  await userEvent.setup().type(screen.getByLabelText('Your word'), 'Paris')
  expect(screen.getByRole('button', { name: 'Submit word' })).toBeDisabled()
  view.rerender(
    <GameControls state={state} playerId="host" busy={false} send={vi.fn()} />,
  )
  expect(screen.getByLabelText('Your word')).toHaveValue('Paris')
  expect(screen.getByRole('button', { name: 'Submit word' })).toBeEnabled()
})
test('server updates replace phase, scores and active-player highlights', () => {
  const hook = vi.spyOn(stateHook, 'useGameState').mockReturnValue({
    state: game,
    connection: 'connected',
    error: null,
    receivedAt: 0,
  })
  const view = render(
    <MemoryRouter>
      <GameView id={game.id} playerId="bob" />
    </MemoryRouter>,
  )
  const scores = screen.getByLabelText('Match scores')
  expect(within(scores).getByText('Active turn · You')).toBeVisible()
  hook.mockReturnValue({
    state: {
      ...game,
      version: '6',
      players: game.players.map((player) => ({ ...player, score: 50 })),
      round: { ...game.round!, status: 'completed' },
    },
    connection: 'connected',
    error: null,
    receivedAt: 1,
  })
  view.rerender(
    <MemoryRouter>
      <GameView id={game.id} playerId="bob" />
    </MemoryRouter>,
  )
  expect(screen.getByRole('heading', { name: 'Round complete' })).toBeVisible()
  expect(scores).toHaveTextContent('Host: 50 points')
  expect(within(scores).queryByText(/Active turn/)).not.toBeInTheDocument()
  expect(
    screen.queryByRole('button', { name: 'Place bid' }),
  ).not.toBeInTheDocument()
})
test('a rejected server action stays visible even when the recovery read fails', async () => {
  vi.spyOn(stateHook, 'useGameState').mockReturnValue({
    state: game,
    connection: 'connected',
    error: null,
    receivedAt: 0,
  })
  const action = vi
    .spyOn(gamesApi, 'act')
    .mockRejectedValue(
      new ApiError(409, { title: 'The turn changed. Try again.' }),
    )
  vi.spyOn(gameSynchronization, 'refresh').mockRejectedValue(
    new Error('offline'),
  )
  render(
    <MemoryRouter>
      <GameView id={game.id} playerId="bob" />
    </MemoryRouter>,
  )
  await userEvent
    .setup()
    .click(screen.getByRole('button', { name: 'Place bid' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'The turn changed. Try again.',
  )
  expect(action).toHaveBeenCalledWith(
    game.id,
    expect.objectContaining({ expectedVersion: '5', type: 'placeBid' }),
  )
})
