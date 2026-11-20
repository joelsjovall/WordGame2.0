// @vitest-environment jsdom
import { afterEach, expect, test, vi } from 'vitest'
import { connectMatchmaking } from './matchmakingConnection.ts'
import { guestsApi } from './api/guests.ts'
import { matchmakingApi, type MatchmakingState } from './api/matchmaking.ts'
import { gamesApi } from './api/games.ts'
import { ApiError } from './api/client.ts'
import {
  idleMatchmaking as idle,
  searchingMatchmaking as searching,
  foundMatchmaking as found,
} from '../../tests/fixtures/matchmaking.ts'
import { gameFixture } from '../../tests/fixtures/game.ts'

class Events extends EventTarget {
  static instances: Events[] = []
  readyState = 0
  onopen: (() => void) | null = null
  onerror: (() => void) | null = null
  close = vi.fn(() => {
    this.readyState = 2
  })
  url: string
  options: EventSourceInit
  constructor(url: string, options: EventSourceInit) {
    super()
    this.url = url
    this.options = options
    Events.instances.push(this)
  }
  opened() {
    this.readyState = 1
    this.onopen?.()
  }
  send(state: unknown) {
    this.readyState = 1
    this.dispatchEvent(
      new MessageEvent('matchmaking', { data: JSON.stringify(state) }),
    )
  }
  fail(closed = false) {
    this.readyState = closed ? 2 : 0
    this.onerror?.()
  }
}
const connections: ReturnType<typeof connectMatchmaking>[] = []
afterEach(() => {
  connections.splice(0).forEach((connection) => connection.stop())
  Events.instances = []
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  vi.useRealTimers()
})
function setup() {
  vi.stubGlobal('EventSource', Events)
  vi.spyOn(guestsApi, 'ensure').mockResolvedValue({ playerId: 'host' })
  const status = vi.spyOn(matchmakingApi, 'status').mockResolvedValue(idle)
  vi.spyOn(gamesApi, 'getState').mockResolvedValue(gameFixture)
  return status
}
async function start() {
  const listener = vi.fn()
  const connection = connectMatchmaking(listener)
  connections.push(connection)
  await vi.waitFor(() => expect(Events.instances).toHaveLength(1))
  const source = Events.instances[0]!
  source.opened()
  await Promise.resolve()
  return {
    listener,
    connection,
    source,
    snapshot: () => listener.mock.calls.at(-1)![0],
  }
}

test('one credentialed subscription restores state and live updates do not poll', async () => {
  const status = setup()
  const view = await start()
  expect(view.source.options).toEqual({ withCredentials: true })
  expect(view.source.url).toContain('/api/v1/matchmaking/events')
  view.source.send(searching)
  expect(view.snapshot().state).toEqual(searching)
  vi.useFakeTimers()
  const calls = status.mock.calls.length
  await vi.advanceTimersByTimeAsync(90000)
  expect(status).toHaveBeenCalledTimes(calls)
  expect(guestsApi.ensure).toHaveBeenCalledOnce()
})

test('a late status read cannot replace a newer match-found event', async () => {
  const status = setup()
  const view = await start()
  let finish!: (state: MatchmakingState) => void
  status.mockImplementation(
    () =>
      new Promise((resolve) => {
        finish = resolve
      }),
  )
  view.source.fail()
  await vi.waitFor(() => expect(finish).toBeDefined())
  view.source.send(found)
  finish(searching)
  await vi.waitFor(() => expect(view.snapshot().opponent).toBe('Bob'))
  expect(view.snapshot().state).toEqual(found)
})

test('duplicate clicks send one write and a stale join response cannot regress live state', async () => {
  setup()
  const view = await start()
  let finish!: (state: MatchmakingState) => void
  const join = vi.spyOn(matchmakingApi, 'join').mockImplementation(
    () =>
      new Promise((resolve) => {
        finish = resolve
      }),
  )
  const pending = view.connection.join('Alice')
  await view.connection.join('Alice')
  expect(join).toHaveBeenCalledOnce()
  expect(view.snapshot().busy).toBe(true)
  view.source.send(found)
  finish(searching)
  await pending
  expect(view.snapshot().state).toEqual(found)
  expect(view.snapshot().busy).toBe(false)
})

test('closed streams reopen and late callbacks from the old source are ignored', async () => {
  const status = setup()
  const view = await start()
  vi.useFakeTimers()
  status.mockResolvedValue(found)
  view.source.fail(true)
  await vi.advanceTimersByTimeAsync(2000)
  expect(Events.instances).toHaveLength(2)
  Events.instances[1]!.send(found)
  view.source.send({ ...searching, status: 'expired' })
  expect(view.snapshot().state?.status).toBe('matched')
  expect(guestsApi.ensure).toHaveBeenCalledOnce()
})

test('an unchanged heartbeat during a join does not discard the successful response', async () => {
  setup()
  const view = await start()
  let finish!: (state: MatchmakingState) => void
  vi.spyOn(matchmakingApi, 'join').mockImplementation(
    () =>
      new Promise((resolve) => {
        finish = resolve
      }),
  )
  const pending = view.connection.join('Alice')
  view.source.send(idle)
  finish(searching)
  await pending
  expect(view.snapshot().state).toEqual(searching)
})

test('native reconnection does not open a parallel stream and recovery stops when live', async () => {
  const status = setup()
  const view = await start()
  vi.useFakeTimers()
  view.source.fail()
  await vi.advanceTimersByTimeAsync(2000)
  expect(Events.instances).toHaveLength(1)
  view.source.opened()
  view.source.send(searching)
  await vi.advanceTimersByTimeAsync(0)
  const calls = status.mock.calls.length
  await vi.advanceTimersByTimeAsync(90000)
  expect(status).toHaveBeenCalledTimes(calls)
})

test('expired credentials stop recovery and never automatically create another identity or retry writes', async () => {
  const status = setup()
  const view = await start()
  const join = vi.spyOn(matchmakingApi, 'join')
  status.mockRejectedValue(new ApiError(401, { title: 'Expired' }))
  view.source.fail()
  await vi.waitFor(() => expect(view.snapshot().connection).toBe('stopped'))
  vi.useFakeTimers()
  const calls = status.mock.calls.length
  await vi.advanceTimersByTimeAsync(90000)
  await view.connection.join('Alice')
  expect(status).toHaveBeenCalledTimes(calls)
  expect(join).not.toHaveBeenCalled()
  expect(guestsApi.ensure).toHaveBeenCalledOnce()
  expect(view.source.close).toHaveBeenCalledOnce()
})

test('failed cancellation keeps a clear error and retrieves the authoritative selected match', async () => {
  const status = setup()
  const view = await start()
  view.source.send(searching)
  status.mockResolvedValue(found)
  const cancel = vi
    .spyOn(matchmakingApi, 'cancel')
    .mockRejectedValue(
      new ApiError(409, { title: 'An opponent has already been selected.' }),
    )
  await view.connection.cancel()
  expect(cancel).toHaveBeenCalledOnce()
  expect(view.snapshot().state?.status).toBe('matched')
  expect(view.snapshot().actionError).toContain('already been selected')
})

test('malformed events recover state and cleanup prevents stale updates or recovery requests', async () => {
  const status = setup()
  const view = await start()
  view.source.send({ status: 'unexpected' })
  await vi.waitFor(() => expect(view.snapshot().connection).toBe('connected'))
  expect(view.snapshot().state).toEqual(idle)
  view.connection.stop()
  const calls = status.mock.calls.length
  view.source.send(found)
  window.dispatchEvent(new Event('online'))
  expect(status).toHaveBeenCalledTimes(calls)
  expect(view.snapshot().state).toEqual(idle)
})
