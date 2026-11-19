// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, test, vi } from 'vitest'
import { LobbyMembers } from './LobbyMembers.tsx'
import { lobbyFixture as lobby } from '../../../tests/fixtures/lobby.ts'

test('members show authoritative names, host, presence and readiness; only host gets removal controls', async () => {
  const remove = vi.fn()
  const user = userEvent.setup()
  const view = render(
    <LobbyMembers
      lobby={{
        ...lobby,
        players: lobby.players.map((player) => ({
          ...player,
          isReady: player.id === 'host',
          isConnected: player.id === 'host',
        })),
      }}
      playerId="host"
      busy={false}
      remove={remove}
    />,
  )
  expect(screen.getByLabelText('Lobby players')).toHaveTextContent(
    'Host (host) — Connected; Ready',
  )
  expect(screen.getByLabelText('Lobby players')).toHaveTextContent(
    'Bob — Reconnecting; Not ready',
  )
  expect(
    screen.queryByRole('button', { name: 'Remove Host' }),
  ).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Remove Bob' }))
  expect(remove).toHaveBeenCalledWith('bob')
  view.rerender(
    <LobbyMembers lobby={lobby} playerId="bob" busy={false} remove={remove} />,
  )
  expect(
    screen.queryByRole('button', { name: /Remove/ }),
  ).not.toBeInTheDocument()
})
test('removal is disabled while pending and hidden after match startup', () => {
  const view = render(
    <LobbyMembers lobby={lobby} playerId="host" busy={true} remove={vi.fn()} />,
  )
  expect(screen.getByRole('button', { name: 'Remove Bob' })).toBeDisabled()
  view.rerender(
    <LobbyMembers
      lobby={{ ...lobby, status: 'starting' }}
      playerId="host"
      busy={false}
      remove={vi.fn()}
    />,
  )
  expect(
    screen.queryByRole('button', { name: /Remove/ }),
  ).not.toBeInTheDocument()
})
