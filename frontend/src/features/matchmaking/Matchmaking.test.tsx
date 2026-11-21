// @vitest-environment jsdom
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import * as matchmakingHook from './useMatchmaking.ts'
import { OnlineModePage } from '../../pages/OnlineModePage.tsx'
import { SearchTimer } from './SearchTimer.tsx'
import { emptyMatchmaking } from '../../services/matchmakingConnection.ts'
import {
  idleMatchmaking as idle,
  searchingMatchmaking as searching,
  foundMatchmaking as found,
} from '../../../tests/fixtures/matchmaking.ts'

type Model = ReturnType<typeof matchmakingHook.useMatchmaking>
function model(overrides: Partial<Model> = {}): Model {
  return {
    ...emptyMatchmaking,
    connection: 'connected',
    state: idle,
    join: vi.fn(async () => {}),
    cancel: vi.fn(async () => {}),
    respond: vi.fn(async () => {}),
    ...overrides,
  }
}
function Target() {
  return <h1>Game route: {useLocation().pathname}</h1>
}
function Page() {
  return (
    <MemoryRouter initialEntries={['/online']}>
      <Routes>
        <Route path="/online" element={<OnlineModePage />} />
        <Route path="/online/games/:gameId" element={<Target />} />
      </Routes>
    </MemoryRouter>
  )
}
afterEach(() => {
  vi.restoreAllMocks()
  vi.useRealTimers()
})

test('players choose their name and start Online matchmaking without an account', async () => {
  const current = model()
  vi.spyOn(matchmakingHook, 'useMatchmaking').mockReturnValue(current)
  render(<Page />)
  const user = userEvent.setup()
  expect(screen.getByRole('button', { name: 'Play Online' })).toBeDisabled()
  await user.type(screen.getByLabelText('Your display name'), ' Alice ')
  await user.click(screen.getByRole('button', { name: 'Play Online' }))
  expect(current.join).toHaveBeenCalledWith('Alice')
})

test('searching displays elapsed time and cancellation while busy/recovering disables writes', async () => {
  const current = model({ state: searching })
  const hook = vi
    .spyOn(matchmakingHook, 'useMatchmaking')
    .mockReturnValue(current)
  const view = render(<Page />)
  expect(
    screen.getByRole('heading', { name: 'Finding your next rival' }),
  ).toBeVisible()
  expect(screen.getByText(/Search time:/)).toBeVisible()
  await userEvent
    .setup()
    .click(screen.getByRole('button', { name: 'Cancel Search' }))
  expect(current.cancel).toHaveBeenCalledOnce()
  hook.mockReturnValue(model({ state: searching, busy: true }))
  view.rerender(<Page />)
  expect(screen.getByRole('button', { name: 'Cancel Search' })).toBeDisabled()
  hook.mockReturnValue(
    model({
      state: searching,
      connection: 'recovering',
      error: 'Connection interrupted. Reconnecting…',
    }),
  )
  view.rerender(<Page />)
  expect(screen.getByRole('alert')).toHaveTextContent('Reconnecting')
  expect(screen.getByRole('button', { name: 'Cancel Search' })).toBeDisabled()
})

test('match found renders opponent text safely and supports accept/decline without navigating early', async () => {
  const opponent = '<img src=x onerror=alert(1)>'
  const current = model({ state: found, opponent })
  const hook = vi
    .spyOn(matchmakingHook, 'useMatchmaking')
    .mockReturnValue(current)
  const view = render(<Page />)
  expect(screen.getByRole('heading', { name: 'Match found!' })).toBeVisible()
  expect(screen.getByText(opponent)).toBeVisible()
  expect(view.container.querySelector('img')).toBeNull()
  const user = userEvent.setup()
  await user.click(screen.getByRole('button', { name: 'Accept match' }))
  expect(current.respond).toHaveBeenCalledWith('accept')
  await user.click(screen.getByRole('button', { name: 'Decline match' }))
  expect(current.respond).toHaveBeenCalledWith('decline')
  hook.mockReturnValue(
    model({ state: { ...found, hasAccepted: true }, opponent }),
  )
  view.rerender(<Page />)
  expect(screen.getByRole('button', { name: 'Accepted' })).toBeDisabled()
  expect(screen.getByText(/Waiting for your opponent/)).toBeVisible()
})

test.each(['expired', 'cancelled', 'failed'] as const)(
  '%s searches show a message and allow an explicit new search',
  async (status) => {
    const current = model({ state: { ...searching, status } })
    vi.spyOn(matchmakingHook, 'useMatchmaking').mockReturnValue(current)
    render(<Page />)
    expect(
      screen.getByRole('status', { name: 'Matchmaking feedback' }),
    ).toHaveTextContent(status === 'failed' ? 'couldn’t create' : status)
    expect(current.join).not.toHaveBeenCalled()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Your display name'), 'Alice')
    await user.click(screen.getByRole('button', { name: 'Play Online' }))
    expect(current.join).toHaveBeenCalledOnce()
  },
)

test('only a ready assignment navigates to its exact game URL', async () => {
  const hook = vi
    .spyOn(matchmakingHook, 'useMatchmaking')
    .mockReturnValue(model({ state: found }))
  const view = render(<Page />)
  expect(screen.queryByText(/Game route:/)).not.toBeInTheDocument()
  hook.mockReturnValue(
    model({
      state: { ...found, status: 'ready', hasAccepted: true, expiresAt: null },
    }),
  )
  view.rerender(<Page />)
  await waitFor(() =>
    expect(
      screen.getByRole('heading', {
        name: `Game route: /online/games/${found.gameId}`,
      }),
    ).toBeVisible(),
  )
})

test('initial loading and terminal session errors have clear recovery navigation', () => {
  const hook = vi
    .spyOn(matchmakingHook, 'useMatchmaking')
    .mockReturnValue(model({ state: null, connection: 'connecting' }))
  const view = render(<Page />)
  expect(
    screen.getByRole('status', { name: 'Matchmaking feedback' }),
  ).toHaveTextContent('Connecting')
  hook.mockReturnValue(
    model({
      state: null,
      connection: 'stopped',
      error: 'Your guest session is no longer available.',
    }),
  )
  view.rerender(<Page />)
  expect(screen.getByRole('alert')).toHaveTextContent('no longer available')
  expect(screen.getByRole('link', { name: /WordGame/ })).toHaveAttribute(
    'href',
    '/',
  )
})

test('search timer uses elapsed time and clamps expired countdowns to zero', () => {
  vi.useFakeTimers()
  const start = new Date('2026-11-20T12:00:00Z')
  vi.setSystemTime(start)
  const view = render(<SearchTimer timestamp={start.toISOString()} />)
  act(() => vi.advanceTimersByTime(65000))
  expect(screen.getByText('01:05')).toBeVisible()
  view.rerender(<SearchTimer timestamp={start.toISOString()} countdown />)
  expect(screen.getByText('00:00')).toBeVisible()
})
