// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import { useOnlineContinue } from './useOnlineContinue.ts'
import {
  rematchesApi,
  type RematchState,
} from '../../services/api/rematches.ts'
import { gameFixture } from '../../../tests/fixtures/game.ts'

afterEach(() => vi.restoreAllMocks())

test('revision changes cancel stale rematch reads and unmount aborts the active read', async () => {
  const idle: RematchState = {
    sourceGameId: gameFixture.id,
    status: 'idle',
    acceptedPlayerIds: [],
    expiresAt: null,
    gameId: null,
  }
  let resolveFirst!: (value: RematchState) => void
  const first = new Promise<RematchState>((resolve) => {
    resolveFirst = resolve
  })
  const current: RematchState = {
    ...idle,
    status: 'pending',
    acceptedPlayerIds: ['host'],
  }
  const get = vi
    .spyOn(rematchesApi, 'get')
    .mockReturnValueOnce(first)
    .mockResolvedValueOnce(current)
  const view = renderHook(
    ({ version }) =>
      useOnlineContinue(
        { ...gameFixture, status: 'completed', version },
        'bob',
      ),
    {
      initialProps: { version: '1' },
      wrapper: ({ children }) => <MemoryRouter>{children}</MemoryRouter>,
    },
  )
  await waitFor(() => expect(get).toHaveBeenCalledTimes(1))
  const firstSignal = get.mock.calls[0]![1]!
  view.rerender({ version: '2' })
  expect(firstSignal.aborted).toBe(true)
  await waitFor(() => expect(view.result.current.rematch).toEqual(current))
  await act(async () => resolveFirst(idle))
  expect(view.result.current.rematch).toEqual(current)
  const currentSignal = get.mock.calls[1]![1]!
  view.unmount()
  expect(currentSignal.aborted).toBe(true)
})
