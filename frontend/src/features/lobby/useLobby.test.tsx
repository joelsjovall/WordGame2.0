// @vitest-environment jsdom
import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { ApiError } from '../../services/api/client.ts'
import { guestsApi } from '../../services/api/guests.ts'
import { lobbiesApi } from '../../services/api/lobbies.ts'
import { useLobby } from './useLobby.ts'
import { lobbyFixture as lobby } from '../../../tests/fixtures/lobby.ts'

class Events extends EventTarget {
  static latest: Events
  close = vi.fn()
  onerror: (() => void) | null = null
  url: string
  options: EventSourceInit
  constructor(url: string, options: EventSourceInit) {
    super()
    this.url = url
    this.options = options
    Events.latest = this
  }
  send(value: unknown) {
    this.dispatchEvent(
      new MessageEvent('lobby', { data: JSON.stringify(value) }),
    )
  }
}
afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})
function setup() {
  vi.stubGlobal('EventSource', Events)
  vi.spyOn(guestsApi, 'ensure').mockResolvedValue({ playerId: 'host' })
  return vi.spyOn(lobbiesApi, 'get').mockResolvedValue(lobby)
}
test('live snapshots never regress and navigation releases the event stream', async () => {
  setup()
  const view = renderHook(() => useLobby(lobby.id))
  await waitFor(() => expect(view.result.current.lobby).toEqual(lobby))
  expect(Events.latest.options).toEqual({ withCredentials: true })
  await act(async () =>
    Events.latest.send({
      ...lobby,
      version: '3',
      settings: { ...lobby.settings, challengeSeconds: 90 },
    }),
  )
  await act(async () => Events.latest.send(lobby))
  expect(view.result.current.lobby?.version).toBe('3')
  expect(view.result.current.connection).toBe('Live')
  view.unmount()
  expect(Events.latest.close).toHaveBeenCalledOnce()
})
test('recovery reads authoritative state and stops on lost membership', async () => {
  const get = setup()
  const view = renderHook(() => useLobby(lobby.id))
  await waitFor(() => expect(view.result.current.lobby).toEqual(lobby))
  get.mockRejectedValue(new ApiError(404, { title: 'Lobby unavailable.' }))
  await act(async () => Events.latest.onerror?.())
  await waitFor(() =>
    expect(view.result.current.error).toContain(
      'This lobby is no longer available.',
    ),
  )
  expect(Events.latest.close).toHaveBeenCalledOnce()
})
test('an unauthorized initial read never opens an event subscription', async () => {
  const get = setup().mockRejectedValue(
    new ApiError(401, { title: 'Session expired.' }),
  )
  const source = vi.fn()
  vi.stubGlobal('EventSource', source)
  const view = renderHook(() => useLobby(lobby.id))
  await waitFor(() =>
    expect(view.result.current.error).toBe('Session expired.'),
  )
  expect(get).toHaveBeenCalledOnce()
  expect(source).not.toHaveBeenCalled()
})

test('malformed or unrelated updates recover without replacing valid state', async () => {
  const get = setup()
  const view = renderHook(() => useLobby(lobby.id))
  await waitFor(() => expect(view.result.current.lobby).toEqual(lobby))
  await act(async () => Events.latest.send({ ...lobby, version: 'broken' }))
  await waitFor(() => expect(get).toHaveBeenCalledTimes(2))
  await act(async () =>
    Events.latest.send({ ...lobby, id: 'another-room', version: '3' }),
  )
  await waitFor(() => expect(get).toHaveBeenCalledTimes(3))
  expect(view.result.current.lobby).toEqual(lobby)
})

test('removal closes the stream and revokes the active lobby view', async () => {
  setup()
  const view = renderHook(() => useLobby(lobby.id))
  await waitFor(() => expect(view.result.current.lobby).toEqual(lobby))
  await act(async () => Events.latest.dispatchEvent(new Event('removed')))
  expect(view.result.current.unavailable).toBe(true)
  expect(view.result.current.error).toBe(
    'You have left or been removed from this lobby.',
  )
  expect(Events.latest.close).toHaveBeenCalledOnce()
  await act(async () => Events.latest.send({ ...lobby, version: '99' }))
  expect(view.result.current.unavailable).toBe(true)
  expect(view.result.current.lobby?.version).toBe(lobby.version)
})

test('a late recovery response cannot clear removal or restore the active view', async () => {
  const get = setup()
  const view = renderHook(() => useLobby(lobby.id))
  await waitFor(() => expect(view.result.current.lobby).toEqual(lobby))
  let resolve!: (value: typeof lobby) => void
  get.mockImplementation(
    () =>
      new Promise((finish) => {
        resolve = finish
      }),
  )
  await act(async () => Events.latest.onerror?.())
  await act(async () => Events.latest.dispatchEvent(new Event('removed')))
  await act(async () => resolve({ ...lobby, version: '100' }))
  expect(view.result.current.unavailable).toBe(true)
  expect(view.result.current.error).toBe(
    'You have left or been removed from this lobby.',
  )
  expect(view.result.current.lobby?.version).toBe(lobby.version)
})
