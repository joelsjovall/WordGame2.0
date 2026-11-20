// @vitest-environment jsdom
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import { OnlineContinue } from './OnlineContinue.tsx'
import {
  rematchesApi,
  type RematchState,
} from '../../services/api/rematches.ts'
import { matchmakingApi } from '../../services/api/matchmaking.ts'
import { gameFixture } from '../../../tests/fixtures/game.ts'

const state = { ...gameFixture, status: 'completed' as const }
const idle: RematchState = {
  sourceGameId: state.id,
  status: 'idle',
  acceptedPlayerIds: [],
  expiresAt: null,
  gameId: null,
}
afterEach(() => vi.restoreAllMocks())
function show() {
  return render(
    <MemoryRouter initialEntries={['/result']}>
      <Routes>
        <Route
          path="/result"
          element={<OnlineContinue state={state} playerId="bob" />}
        />
        <Route path="/online" element={<p>Queue page</p>} />
        <Route path="/online/games/:id" element={<p>Fresh match</p>} />
      </Routes>
    </MemoryRouter>,
  )
}
test('request waits for the opponent and acceptance navigates to a fresh match', async () => {
  const get = vi.spyOn(rematchesApi, 'get').mockResolvedValue(idle)
  const respond = vi.spyOn(rematchesApi, 'respond').mockResolvedValue({
    ...idle,
    status: 'pending',
    acceptedPlayerIds: ['bob'],
  })
  const view = show()
  await userEvent
    .setup()
    .click(await screen.findByRole('button', { name: 'Request Rematch' }))
  expect(respond).toHaveBeenCalledWith(state.id, 'request')
  expect(screen.getByText(/Waiting for your opponent/)).toBeInTheDocument()
  expect(
    screen.queryByRole('button', { name: 'Accept Rematch' }),
  ).not.toBeInTheDocument()
  get.mockResolvedValue({ ...idle, status: 'ready', gameId: 'fresh' })
  // A source SSE revision causes a fresh authoritative negotiation read.
  view.unmount()
  show()
  expect(await screen.findByText('Fresh match')).toBeInTheDocument()
})
test('opponent can accept or decline and expired requests show clear feedback', async () => {
  vi.spyOn(rematchesApi, 'get').mockResolvedValue({
    ...idle,
    status: 'pending',
    acceptedPlayerIds: ['host'],
  })
  const respond = vi
    .spyOn(rematchesApi, 'respond')
    .mockResolvedValue({ ...idle, status: 'expired' })
  show()
  expect(
    await screen.findByRole('button', { name: 'Decline Rematch' }),
  ).toBeEnabled()
  await userEvent
    .setup()
    .click(screen.getByRole('button', { name: 'Accept Rematch' }))
  expect(respond).toHaveBeenCalledWith(state.id, 'accept')
  expect(
    await screen.findByText('The rematch request expired.'),
  ).toBeInTheDocument()
})
test('Find New Opponent declines negotiation before joining the queue', async () => {
  vi.spyOn(rematchesApi, 'get').mockResolvedValue(idle)
  const respond = vi
    .spyOn(rematchesApi, 'respond')
    .mockResolvedValue({ ...idle, status: 'declined' })
  const join = vi.spyOn(matchmakingApi, 'join').mockResolvedValue({
    searchId: 'new',
    status: 'searching',
    createdAt: null,
    expiresAt: null,
    matchId: null,
    gameId: null,
    hasAccepted: false,
  })
  show()
  await userEvent
    .setup()
    .click(screen.getByRole('button', { name: 'Find New Opponent' }))
  await waitFor(() => expect(join).toHaveBeenCalled())
  expect(respond).toHaveBeenCalledWith(state.id, 'decline')
  expect(await screen.findByText('Queue page')).toBeInTheDocument()
})
test('errors retain a retry path without claiming success', async () => {
  vi.spyOn(rematchesApi, 'get').mockRejectedValue(new Error('offline'))
  show()
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Unable to load rematch options',
  )
  expect(screen.getByRole('button', { name: 'Retry' })).toBeEnabled()
  expect(
    screen.getByRole('button', { name: 'Find New Opponent' }),
  ).toBeEnabled()
})
