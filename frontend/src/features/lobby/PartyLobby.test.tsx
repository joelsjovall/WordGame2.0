// @vitest-environment jsdom
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import App from '../../App.tsx'
import * as lobbyHook from './useLobby.ts'
import { lobbiesApi } from '../../services/api/lobbies.ts'
import { lobbyFixture as lobby } from '../../../tests/fixtures/lobby.ts'
import { LobbyMembers } from './LobbyMembers.tsx'
import { InvitationPanel } from './InvitationPanel.tsx'

afterEach(() => vi.restoreAllMocks())
function state(overrides: Partial<ReturnType<typeof lobbyHook.useLobby>> = {}) {
  return {
    lobby: { ...lobby, canStart: true },
    activity: '',
    playerId: 'host',
    connection: 'Live',
    error: '',
    unavailable: false,
    accept: vi.fn(),
    ...overrides,
  }
}
function room() {
  return render(
    <MemoryRouter initialEntries={[`/lobbies/${lobby.id}`]}>
      <App />
    </MemoryRouter>,
  )
}
test('host controls are separated and unsaved edits block start until authoritative settings arrive', async () => {
  const current = state()
  const hook = vi.spyOn(lobbyHook, 'useLobby').mockReturnValue(current)
  const configure = vi.spyOn(lobbiesApi, 'configure').mockResolvedValue({
    ...lobby,
    version: '2',
    settings: { ...lobby.settings, roundCount: 10 },
  })
  const start = vi.spyOn(lobbiesApi, 'start').mockResolvedValue(lobby)
  const user = userEvent.setup()
  const view = room()
  expect(screen.getByRole('region', { name: 'Host controls' })).toBeVisible()
  expect(screen.getByRole('button', { name: 'Start match' })).toBeEnabled()
  await user.type(screen.getByLabelText('Total rounds'), '10')
  expect(screen.getByRole('button', { name: 'Start match' })).toBeDisabled()
  expect(screen.getByText('Save your settings before starting.')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Save settings' }))
  expect(configure).toHaveBeenCalledWith(lobby.id, lobby.version, {
    ...lobby.settings,
    roundCount: 10,
  })
  await waitFor(() => expect(current.accept).toHaveBeenCalled())
  hook.mockReturnValue(
    state({
      lobby: {
        ...lobby,
        canStart: true,
        version: '2',
        settings: { ...lobby.settings, roundCount: 10 },
      },
    }),
  )
  view.rerender(
    <MemoryRouter initialEntries={[`/lobbies/${lobby.id}`]}>
      <App />
    </MemoryRouter>,
  )
  await user.click(screen.getByRole('button', { name: 'Start match' }))
  expect(start).toHaveBeenCalledWith(lobby.id, '2')
})
test('guests see server settings and activity without host controls and readiness sends the server version', async () => {
  vi.spyOn(lobbyHook, 'useLobby').mockReturnValue(
    state({ playerId: 'bob', activity: 'Taylor joined the lobby.' }),
  )
  const ready = vi.spyOn(lobbiesApi, 'setReady').mockResolvedValue(lobby)
  const user = userEvent.setup()
  room()
  expect(
    screen.queryByRole('region', { name: 'Host controls' }),
  ).not.toBeInTheDocument()
  expect(
    screen.queryByRole('button', { name: 'Save settings' }),
  ).not.toBeInTheDocument()
  expect(
    screen.queryByRole('button', { name: 'Start match' }),
  ).not.toBeInTheDocument()
  expect(screen.getByRole('region', { name: 'Match settings' })).toBeVisible()
  expect(screen.getByRole('log')).toHaveTextContent('Taylor joined the lobby.')
  expect(screen.getByRole('button', { name: 'Ready' })).toHaveAttribute(
    'aria-pressed',
    'false',
  )
  await user.click(screen.getByRole('button', { name: 'Ready' }))
  expect(ready).toHaveBeenCalledWith(lobby.id, lobby.version, true)
})
test('loading and terminal errors provide clear states without active room controls', () => {
  const hook = vi
    .spyOn(lobbyHook, 'useLobby')
    .mockReturnValue(state({ lobby: null }))
  const view = room()
  expect(screen.getByRole('status')).toHaveTextContent('Loading lobby…')
  expect(
    screen.queryByRole('button', { name: 'Ready' }),
  ).not.toBeInTheDocument()
  hook.mockReturnValue(
    state({
      unavailable: true,
      error: 'You have been removed from this lobby.',
    }),
  )
  view.rerender(
    <MemoryRouter initialEntries={[`/lobbies/${lobby.id}`]}>
      <App />
    </MemoryRouter>,
  )
  expect(screen.getByRole('alert')).toHaveTextContent(
    'You have been removed from this lobby.',
  )
  expect(screen.getByRole('link', { name: 'Join a lobby' })).toBeVisible()
  expect(
    screen.queryByRole('button', { name: 'Start match' }),
  ).not.toBeInTheDocument()
})
test('empty player lists have an invitation hint', () => {
  render(
    <LobbyMembers
      lobby={{ ...lobby, players: [] }}
      playerId="host"
      busy={false}
      remove={vi.fn()}
    />,
  )
  expect(
    screen.getByText(
      'No players are here yet. Share the code to invite friends.',
    ),
  ).toBeVisible()
})
test('copy code and invitation copy the authoritative code and public URL separately', async () => {
  const user = userEvent.setup()
  const write = vi
    .spyOn(navigator.clipboard, 'writeText')
    .mockResolvedValue(undefined)
  render(<InvitationPanel lobby={lobby} />)
  await user.click(screen.getByRole('button', { name: 'Copy code' }))
  expect(write).toHaveBeenLastCalledWith(lobby.lobbyCode)
  expect(screen.getByRole('status')).toHaveTextContent('Lobby code copied.')
  await user.click(screen.getByRole('button', { name: 'Copy invitation link' }))
  expect(write).toHaveBeenLastCalledWith(
    expect.stringContaining(`/join/${lobby.id}/${lobby.lobbyCode}`),
  )
})
