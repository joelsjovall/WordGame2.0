// @vitest-environment jsdom
import { act, render, screen, waitFor } from '@testing-library/react'
import { expect, test, vi } from 'vitest'
import { createGameSynchronization } from '../../services/gameSynchronization.ts'
import type { GameState } from '../../services/api/games.ts'
import { useGameState } from './useGameState.ts'

test('components share synchronization, rerender on updates, and release it on navigation', async () => {
  const id = 'd9ae6b2c-8104-4720-9e56-a2a043fcd119'
  const close = vi.fn()
  const openEvents = vi.fn(
    () =>
      ({
        readyState: 0,
        close,
        addEventListener: vi.fn(),
      }) as unknown as EventSource,
  )
  let revision = '1'
  const getState = vi.fn(async (): Promise<GameState> => ({
    id,
    version: revision,
    status: 'active',
    serverTime: '2026-11-18T12:00:00Z',
    players: [],
    round: null,
    match: { format: 'party', roundCount: 4 },
    result: null,
  }))
  const sync = createGameSynchronization({ getState, openEvents })
  function View() {
    const snapshot = useGameState(id, sync)
    return <span>Revision {snapshot.state?.version ?? 'loading'}</span>
  }
  const rendered = render(
    <>
      <View />
      <View />
    </>,
  )
  await waitFor(() => expect(screen.getAllByText('Revision 1')).toHaveLength(2))
  expect(openEvents).toHaveBeenCalledTimes(1)
  revision = '2'
  await act(() => sync.refresh(id))
  expect(screen.getAllByText('Revision 2')).toHaveLength(2)
  rendered.rerender(<View />)
  expect(close).not.toHaveBeenCalled()
  rendered.unmount()
  expect(close).toHaveBeenCalledTimes(1)
})
