// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import App from '../../App.tsx'
import { lobbiesApi } from '../../services/api/lobbies.ts'
import { ApiError } from '../../services/api/client.ts'
import { lobbyFixture as lobby } from '../../../tests/fixtures/lobby.ts'
import { InvitationPanel } from './InvitationPanel.tsx'
import { lobbyInvitationUrl } from './invitations.ts'

const originalClipboard = Object.getOwnPropertyDescriptor(
  navigator,
  'clipboard',
)
const originalShare = Object.getOwnPropertyDescriptor(navigator, 'share')
afterEach(() => {
  vi.restoreAllMocks()
  for (const [name, descriptor] of [
    ['clipboard', originalClipboard],
    ['share', originalShare],
  ] as const) {
    if (descriptor) Object.defineProperty(navigator, name, descriptor)
    else Reflect.deleteProperty(navigator, name)
  }
})

test('manual joining normalizes pasted codes and renders authoritative membership', async () => {
  const join = vi.spyOn(lobbiesApi, 'join').mockResolvedValue(lobby)
  const user = userEvent.setup()
  render(
    <MemoryRouter initialEntries={['/join']}>
      <App />
    </MemoryRouter>,
  )
  fireEvent.change(screen.getByLabelText('Lobby code'), {
    target: { value: ' abc234 ' },
  })
  await user.type(screen.getByLabelText('Your display name'), 'Bob')
  await user.click(screen.getByRole('button', { name: 'Join lobby' }))
  expect(join).toHaveBeenCalledWith(
    { lobbyCode: 'ABC234', displayName: 'Bob' },
    expect.any(AbortSignal),
  )
  expect(
    await screen.findByRole('heading', { name: 'You joined the lobby' }),
  ).toBeVisible()
  expect(screen.getByText('Host (host)')).toBeVisible()
  expect(screen.getByLabelText('Invitation link')).toHaveValue(
    lobbyInvitationUrl(lobby),
  )
})

test('invitation URLs bind the expected lobby ID and can switch to manual code entry', async () => {
  const join = vi.spyOn(lobbiesApi, 'join').mockResolvedValue(lobby)
  const user = userEvent.setup()
  const rendered = render(
    <MemoryRouter initialEntries={[`/join/${lobby.id}/ABC234`]}>
      <App />
    </MemoryRouter>,
  )
  expect(screen.getByLabelText('Lobby code')).toHaveAttribute('readonly')
  await user.click(screen.getByRole('link', { name: 'Use a different code' }))
  expect(screen.getByLabelText('Lobby code')).toHaveValue('')
  rendered.unmount()
  render(
    <MemoryRouter initialEntries={[`/join/${lobby.id}/ABC234`]}>
      <App />
    </MemoryRouter>,
  )
  await user.type(screen.getByLabelText('Your display name'), 'Bob')
  await user.click(screen.getByRole('button', { name: 'Join lobby' }))
  expect(join).toHaveBeenCalledWith(
    { lobbyCode: 'ABC234', displayName: 'Bob', lobbyId: lobby.id },
    expect.any(AbortSignal),
  )
})

test('invalid invitation syntax displays a useful recovery link without joining', () => {
  const join = vi.spyOn(lobbiesApi, 'join')
  render(
    <MemoryRouter initialEntries={['/join/not-an-id/ABC234']}>
      <App />
    </MemoryRouter>,
  )
  expect(
    screen.getByRole('heading', { name: 'Invalid invitation' }),
  ).toBeVisible()
  expect(
    screen.getByRole('link', { name: 'enter a lobby code' }),
  ).toHaveAttribute('href', '/join')
  expect(join).not.toHaveBeenCalled()
})

test('join errors stay visible and pending submissions are not duplicated', async () => {
  let reject!: (error: Error) => void
  const join = vi.spyOn(lobbiesApi, 'join').mockImplementation(
    () =>
      new Promise((_, fail) => {
        reject = fail
      }),
  )
  const user = userEvent.setup()
  render(
    <MemoryRouter initialEntries={['/join']}>
      <App />
    </MemoryRouter>,
  )
  await user.type(screen.getByLabelText('Lobby code'), 'ABC234')
  await user.type(screen.getByLabelText('Your display name'), 'Bob')
  const button = screen.getByRole('button', { name: 'Join lobby' })
  fireEvent.submit(button.closest('form')!)
  fireEvent.submit(button.closest('form')!)
  expect(join).toHaveBeenCalledTimes(1)
  await act(async () =>
    reject(new ApiError(409, { title: 'This lobby is full.' })),
  )
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'This lobby is full.',
  )
  expect(screen.getByRole('button', { name: 'Join lobby' })).toBeEnabled()
})

test('navigation aborts pending joining and ignores late responses', async () => {
  let resolve!: (value: typeof lobby) => void
  let signal: AbortSignal | undefined
  vi.spyOn(lobbiesApi, 'join').mockImplementation((_, currentSignal) => {
    signal = currentSignal
    return new Promise((finish) => {
      resolve = finish
    })
  })
  const user = userEvent.setup()
  const rendered = render(
    <MemoryRouter initialEntries={['/join']}>
      <App />
    </MemoryRouter>,
  )
  await user.type(screen.getByLabelText('Lobby code'), 'ABC234')
  await user.type(screen.getByLabelText('Your display name'), 'Bob')
  await user.click(screen.getByRole('button', { name: 'Join lobby' }))
  rendered.unmount()
  expect(signal?.aborted).toBe(true)
  await act(async () => resolve(lobby))
})

test('copy and supported native share use the frontend invitation URL without credentials', async () => {
  const user = userEvent.setup()
  const writeText = vi.fn().mockResolvedValue(undefined)
  const share = vi.fn().mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'clipboard', {
    configurable: true,
    value: { writeText },
  })
  Object.defineProperty(navigator, 'share', {
    configurable: true,
    value: share,
  })
  render(<InvitationPanel lobby={lobby} />)
  await user.click(screen.getByRole('button', { name: 'Copy invitation link' }))
  expect(writeText).toHaveBeenCalledWith(lobbyInvitationUrl(lobby))
  expect(screen.getByRole('status')).toHaveTextContent(
    'Invitation link copied.',
  )
  await user.click(screen.getByRole('button', { name: 'Share invitation' }))
  expect(share).toHaveBeenCalledWith({
    title: 'Join my WordGame lobby',
    url: lobbyInvitationUrl(lobby),
  })
  expect(new URL(lobbyInvitationUrl(lobby)).search).toBe('')
})

test('unavailable clipboard/share leave a selectable link and a clear copy fallback', async () => {
  const user = userEvent.setup()
  Object.defineProperty(navigator, 'clipboard', {
    configurable: true,
    value: undefined,
  })
  Object.defineProperty(navigator, 'share', {
    configurable: true,
    value: undefined,
  })
  render(<InvitationPanel lobby={lobby} />)
  expect(screen.queryByRole('button', { name: 'Share invitation' })).toBeNull()
  await user.click(screen.getByRole('button', { name: 'Copy invitation link' }))
  expect(screen.getByRole('status')).toHaveTextContent(
    'Select and copy the link above.',
  )
  expect(screen.getByLabelText('Invitation link')).toHaveAttribute('readonly')
})

test('cancelling native share is handled without a failure alert', async () => {
  const user = userEvent.setup()
  Object.defineProperty(navigator, 'share', {
    configurable: true,
    value: vi
      .fn()
      .mockRejectedValue(new DOMException('Cancelled', 'AbortError')),
  })
  render(<InvitationPanel lobby={lobby} />)
  await user.click(screen.getByRole('button', { name: 'Share invitation' }))
  await waitFor(() =>
    expect(screen.getByRole('status')).toHaveTextContent('Sharing cancelled.'),
  )
})
