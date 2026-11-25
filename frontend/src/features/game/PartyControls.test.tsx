// @vitest-environment jsdom
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import { GameControls } from './GameControls.tsx'
import { PartyResults } from './PartyResults.tsx'
import { ServerCountdown } from './ServerCountdown.tsx'
import { categoriesApi } from '../../services/api/categories.ts'
import { lobbiesApi } from '../../services/api/lobbies.ts'
import { ApiError } from '../../services/api/client.ts'
import { gameFixture as game } from '../../../tests/fixtures/game.ts'
import { lobbyFixture as lobby } from '../../../tests/fixtures/lobby.ts'

afterEach(() => {
  vi.restoreAllMocks()
  vi.useRealTimers()
})
test('only the active player gets bidding commands and commands do not nominate an actor', async () => {
  const send = vi.fn()
  const user = userEvent.setup()
  const view = render(
    <GameControls state={game} playerId="host" busy={false} send={send} />,
  )
  expect(
    screen.queryByRole('button', { name: 'Call bluff' }),
  ).not.toBeInTheDocument()
  view.rerender(
    <GameControls state={game} playerId="bob" busy={false} send={send} />,
  )
  expect(screen.getByLabelText('Your bid')).toHaveAttribute('min', '2')
  await user.click(screen.getByRole('button', { name: 'Call bluff' }))
  expect(send).toHaveBeenCalledWith({ type: 'callBluff' })
})
test('risk bids send the optional stake to the server', async () => {
  const send = vi.fn()
  const user = userEvent.setup()
  render(<GameControls state={game} playerId="bob" busy={false} send={send} />)
  await user.click(screen.getByRole('checkbox', { name: /Risk bid:/ }))
  await user.click(screen.getByRole('button', { name: 'Place bid' }))
  expect(send).toHaveBeenCalledWith({
    type: 'placeBid',
    wordCount: 2,
    risk: true,
  })
})
test('category controls show the frozen difficulty subset and submit only the category ID', async () => {
  vi.spyOn(categoriesApi, 'list').mockResolvedValue({
    items: [
      { id: 'easy', name: 'Easy category', difficulty: 'easy' },
      { id: 'hard', name: 'Hard category', difficulty: 'hard' },
    ],
  })
  const send = vi.fn()
  const user = userEvent.setup()
  render(
    <GameControls
      state={{
        ...game,
        round: {
          ...game.round!,
          status: 'categorySelection',
          activePlayerId: 'host',
        },
      }}
      playerId="host"
      busy={false}
      send={send}
    />,
  )
  await user.click(
    await screen.findByRole('button', { name: 'Easy category (easy)' }),
  )
  expect(
    screen.queryByRole('button', { name: 'Hard category (hard)' }),
  ).not.toBeInTheDocument()
  expect(send).toHaveBeenCalledWith({
    type: 'selectCategory',
    categoryId: 'easy',
  })
})
test('theme filter narrows visible categories without changing the selected ID', async () => {
  vi.spyOn(categoriesApi, 'list').mockResolvedValue({
    items: [
      {
        id: 'european-capitals',
        name: 'European Capitals',
        difficulty: 'easy',
      },
      {
        id: 'solar-system-planets',
        name: 'Solar System Planets',
        difficulty: 'easy',
      },
    ],
  })
  const send = vi.fn()
  const user = userEvent.setup()
  render(
    <GameControls
      state={{
        ...game,
        round: {
          ...game.round!,
          status: 'categorySelection',
          activePlayerId: 'host',
        },
      }}
      playerId="host"
      busy={false}
      send={send}
    />,
  )
  await screen.findByRole('button', { name: 'Solar System Planets (easy)' })
  await user.selectOptions(
    screen.getByRole('combobox', { name: 'Theme' }),
    'Science & space',
  )
  expect(
    screen.queryByRole('button', { name: 'European Capitals (easy)' }),
  ).toBeNull()
  await user.click(
    screen.getByRole('button', { name: 'Solar System Planets (easy)' }),
  )
  expect(send).toHaveBeenCalledWith({
    type: 'selectCategory',
    categoryId: 'solar-system-planets',
  })
})
test('word challenge controls are bidder-only and observers see authoritative progress', async () => {
  const send = vi.fn()
  const user = userEvent.setup()
  const state = {
    ...game,
    round: {
      ...game.round!,
      status: 'challenge' as const,
      activePlayerId: 'host',
      challenge: {
        id: 'challenge',
        playerId: 'host',
        callerId: 'bob',
        requiredWordCount: 1,
        startedAt: game.serverTime,
        deadline: '2026-11-19T12:00:05Z',
        outcome: 'pending' as const,
        acceptedWordCount: 0,
        submissions: [],
      },
    },
  }
  const view = render(
    <GameControls state={state} playerId="bob" busy={false} send={send} />,
  )
  expect(screen.queryByLabelText('Your word')).not.toBeInTheDocument()
  expect(screen.getByText('0 of 1 words accepted')).toBeVisible()
  view.rerender(
    <GameControls state={state} playerId="host" busy={false} send={send} />,
  )
  await user.type(screen.getByLabelText('Your word'), 'Stockholm')
  await user.click(screen.getByRole('button', { name: 'Submit word' }))
  expect(send).toHaveBeenCalledWith({ type: 'submitWord', word: 'Stockholm' })
})
test('countdown uses server timestamps and never goes below zero', () => {
  vi.useFakeTimers()
  render(
    <ServerCountdown
      deadline="2026-11-19T12:00:03Z"
      serverTime="2026-11-19T12:00:00Z"
    />,
  )
  expect(screen.getByText('3')).toBeVisible()
  act(() => vi.advanceTimersByTime(3400))
  expect(screen.getByText('0')).toBeVisible()
})
const completed = {
  ...lobby,
  status: 'completed' as const,
  lastResult: {
    gameId: game.id,
    format: 'party' as const,
    completedRoundCount: 1,
    isTie: true,
    winnerIds: ['host', 'bob'],
    standings: [
      { playerId: 'host', displayName: 'Host', score: 10, rank: 1 },
      { playerId: 'bob', displayName: 'Bob', score: 10, rank: 1 },
    ],
    bestBluffCall: {
      categoryName: 'European Capitals',
      wordCount: 5,
      bidderName: 'Host',
      callerName: 'Bob',
    },
  },
}
test('results display shared winners and only the host can prepare a rematch', async () => {
  const rematch = vi.spyOn(lobbiesApi, 'rematch').mockResolvedValue(lobby)
  const accept = vi.fn()
  const user = userEvent.setup()
  const view = render(
    <MemoryRouter>
      <PartyResults lobby={completed} playerId="bob" accept={accept} />
    </MemoryRouter>,
  )
  expect(
    screen.getByRole('heading', { name: 'Shared victory: Host, Bob' }),
  ).toBeVisible()
  expect(screen.getByText(/Bob caught Host's 5-word bid/)).toBeVisible()
  expect(
    screen.queryByRole('button', { name: 'Play Again' }),
  ).not.toBeInTheDocument()
  view.rerender(
    <MemoryRouter>
      <PartyResults lobby={completed} playerId="host" accept={accept} />
    </MemoryRouter>,
  )
  await user.click(screen.getByRole('button', { name: 'Play Again' }))
  expect(rematch).toHaveBeenCalledWith(lobby.id, lobby.version)
  expect(accept).toHaveBeenCalledWith(lobby)
})
test('rematch errors are visible and refresh authoritative state without retrying the write', async () => {
  const rematch = vi
    .spyOn(lobbiesApi, 'rematch')
    .mockRejectedValue(new ApiError(409, { title: 'The lobby changed.' }))
  vi.spyOn(lobbiesApi, 'get').mockResolvedValue(completed)
  const user = userEvent.setup()
  render(
    <MemoryRouter>
      <PartyResults lobby={completed} playerId="host" accept={vi.fn()} />
    </MemoryRouter>,
  )
  await user.click(screen.getByRole('button', { name: 'Play Again' }))
  await waitFor(() =>
    expect(screen.getByRole('alert')).toHaveTextContent('The lobby changed.'),
  )
  expect(rematch).toHaveBeenCalledOnce()
})
