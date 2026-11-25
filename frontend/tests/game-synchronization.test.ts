import { afterEach, expect, test, vi } from 'vitest'
import { ApiError } from '../src/services/api/client.ts'
import type { GameState } from '../src/services/api/games.ts'
import { createGameSynchronization } from '../src/services/gameSynchronization.ts'

const gameId = 'd9ae6b2c-8104-4720-9e56-a2a043fcd119'
function state(version = '1'): GameState {
  return {
    id: gameId,
    version,
    status: 'active',
    serverTime: '2026-11-18T12:00:00Z',
    players: [],
    round: null,
    match: { format: 'party', roundCount: 4, sealedOpening: false },
    result: null,
  }
}

class Events {
  readyState = 0
  onopen: (() => void) | null = null
  onerror: (() => void) | null = null
  listeners = new Map<string, (event: { data: string }) => void>()
  close = vi.fn(() => {
    this.readyState = 2
  })
  addEventListener(type: string, listener: (event: { data: string }) => void) {
    this.listeners.set(type, listener)
  }
  open() {
    this.readyState = 1
    this.onopen?.()
  }
  fail() {
    this.readyState = 0
    this.onerror?.()
  }
  emit(version: string, kind = 'updated', id = gameId) {
    this.listeners.get('game')?.({
      data: JSON.stringify({ gameId: id, version, kind }),
    })
  }
}

function setup(
  getState = vi.fn<() => Promise<GameState>>().mockResolvedValue(state()),
) {
  vi.useFakeTimers()
  const events: Events[] = []
  const openEvents = vi.fn(() => {
    const source = new Events()
    events.push(source)
    return source as unknown as EventSource
  })
  let now = 1000
  const sync = createGameSynchronization({
    getState,
    openEvents,
    now: () => now,
    baseUrl: 'https://api.example',
  })
  return {
    sync,
    events,
    openEvents,
    getState,
    advanceClock: (milliseconds: number) => {
      now += milliseconds
    },
  }
}
const flush = () => vi.advanceTimersByTimeAsync(0)
afterEach(() => {
  vi.useRealTimers()
})

test('fallback works when EventSource is unavailable and retries opening one stream', async () => {
  vi.useFakeTimers()
  const getState = vi.fn<() => Promise<GameState>>().mockResolvedValue(state())
  const openEvents = vi.fn((): EventSource => {
    throw new Error('unsupported')
  })
  const sync = createGameSynchronization({ getState, openEvents })
  const release = sync.subscribe(gameId, vi.fn())
  await flush()
  expect(sync.getSnapshot(gameId).state?.version).toBe('1')
  getState.mockResolvedValue(state('2'))
  await vi.advanceTimersByTimeAsync(2000)
  expect(openEvents).toHaveBeenCalledTimes(2)
  expect(sync.getSnapshot(gameId).state?.version).toBe('2')
  release()
  await vi.advanceTimersByTimeAsync(60000)
  expect(openEvents).toHaveBeenCalledTimes(2)
})

test('listeners share one subscription, refresh on new events, and ignore repeated or old revisions', async () => {
  const { sync, events, openEvents, getState } = setup()
  const listener = vi.fn()
  const first = sync.subscribe(gameId, listener)
  const second = sync.subscribe(gameId.toUpperCase(), listener)
  await flush()
  expect(openEvents).toHaveBeenCalledTimes(1)
  expect(openEvents).toHaveBeenCalledWith(
    `https://api.example/api/v1/games/${gameId}/events`,
  )
  const source = events[0]!
  getState.mockResolvedValue(state('2'))
  source.emit('2')
  await flush()
  expect(sync.getSnapshot(gameId).state?.version).toBe('2')
  const calls = getState.mock.calls.length
  source.emit('2')
  source.emit('1')
  source.emit('9', 'updated', 'someone-else')
  source.listeners.get('game')?.({ data: 'not JSON' })
  await flush()
  expect(getState).toHaveBeenCalledTimes(calls)
  first()
  expect(source.close).not.toHaveBeenCalled()
  second()
  second()
  expect(source.close).toHaveBeenCalledTimes(1)
  expect(sync.getSnapshot(gameId).state).toBeNull()
})

test('reopening retrieves latest state and leaves no healthy polling loop', async () => {
  const { sync, events, getState } = setup()
  const release = sync.subscribe(gameId, vi.fn())
  await flush()
  const source = events[0]!
  source.open()
  await flush()
  source.fail()
  await flush()
  expect(sync.getSnapshot(gameId).connection).toBe('recovering')
  getState.mockResolvedValue(state('7'))
  source.open()
  await flush()
  expect(sync.getSnapshot(gameId).state?.version).toBe('7')
  expect(sync.getSnapshot(gameId).connection).toBe('connected')
  const calls = getState.mock.calls.length
  await vi.advanceTimersByTimeAsync(60000)
  expect(getState).toHaveBeenCalledTimes(calls)
  release()
})

test('a permanently closed native stream is replaced once and old callbacks are ignored', async () => {
  const { sync, events, openEvents, getState } = setup()
  const release = sync.subscribe(gameId, vi.fn())
  await flush()
  const original = events[0]!
  original.readyState = 2
  original.onerror?.()
  await flush()
  await vi.advanceTimersByTimeAsync(2000)
  expect(openEvents).toHaveBeenCalledTimes(2)
  expect(original.close).toHaveBeenCalledTimes(1)
  getState.mockResolvedValue(state('9'))
  events[1]!.open()
  await flush()
  const calls = getState.mock.calls.length
  original.emit('10')
  await flush()
  expect(getState).toHaveBeenCalledTimes(calls)
  expect(sync.getSnapshot(gameId).state?.version).toBe('9')
  release()
})

test('offline fallback retries with backoff and stops after recovery', async () => {
  const getState = vi
    .fn<() => Promise<GameState>>()
    .mockRejectedValue(new TypeError('offline'))
  const { sync, events } = setup(getState)
  const release = sync.subscribe(gameId, vi.fn())
  await flush()
  expect(getState).toHaveBeenCalledTimes(1)
  await vi.advanceTimersByTimeAsync(2000)
  expect(getState).toHaveBeenCalledTimes(2)
  await vi.advanceTimersByTimeAsync(3999)
  expect(getState).toHaveBeenCalledTimes(2)
  getState.mockResolvedValue(state('5'))
  await vi.advanceTimersByTimeAsync(1)
  expect(getState).toHaveBeenCalledTimes(3)
  expect(sync.getSnapshot(gameId).state?.version).toBe('5')
  events[0]!.open()
  await flush()
  const calls = getState.mock.calls.length
  await vi.advanceTimersByTimeAsync(90000)
  expect(getState).toHaveBeenCalledTimes(calls)
  release()
})

test.each([401, 403, 404])(
  'HTTP %s closes events, clears private state, and stops retries',
  async (status) => {
    const { sync, events, getState } = setup(
      vi.fn<() => Promise<GameState>>().mockRejectedValue(new ApiError(status)),
    )
    const release = sync.subscribe(gameId, vi.fn())
    await flush()
    expect(events[0]!.close).toHaveBeenCalledTimes(1)
    expect(sync.getSnapshot(gameId).connection).toBe('error')
    expect(sync.getSnapshot(gameId).state).toBeNull()
    await vi.advanceTimersByTimeAsync(90000)
    expect(getState).toHaveBeenCalledTimes(1)
    release()
  },
)

test('unsubscribe aborts reads and ignores late callbacks, including repeated cleanup', async () => {
  let resolve!: (value: GameState) => void
  let signal: AbortSignal | undefined
  const { sync, events } = setup(
    vi.fn().mockImplementation((_id: string, currentSignal: AbortSignal) => {
      signal = currentSignal
      return new Promise<GameState>((finish) => {
        resolve = finish
      })
    }),
  )
  const listener = vi.fn()
  const release = sync.subscribe(gameId, listener)
  release()
  expect(signal?.aborted).toBe(true)
  resolve(state())
  events[0]!.open()
  await flush()
  expect(listener).not.toHaveBeenCalled()
  expect(sync.getSnapshot(gameId).state).toBeNull()
  const nextRelease = sync.subscribe(gameId, listener)
  release()
  expect(events[1]!.close).not.toHaveBeenCalled()
  nextRelease()
})

test('removal stops subscriptions and countdown uses server time plus monotonic elapsed time', async () => {
  const { sync, events, advanceClock, getState } = setup()
  const release = sync.subscribe(gameId, vi.fn())
  await flush()
  const deadline = '2026-11-18T12:00:30Z'
  expect(sync.remaining(gameId, deadline)).toBe(30000)
  advanceClock(5000)
  vi.setSystemTime(new Date('2040-01-01'))
  expect(sync.remaining(gameId, deadline)).toBe(25000)
  advanceClock(30000)
  expect(sync.remaining(gameId, deadline)).toBe(0)
  expect(sync.remaining(gameId, 'invalid')).toBeNull()
  events[0]!.emit('1', 'removed')
  expect(sync.getSnapshot(gameId).connection).toBe('stopped')
  expect(sync.remaining(gameId, deadline)).toBeNull()
  await vi.advanceTimersByTimeAsync(60000)
  expect(getState).toHaveBeenCalledTimes(1)
  release()
})

test('versions retain precision beyond JavaScript safe integers', async () => {
  const { sync, events, getState } = setup(
    vi
      .fn<() => Promise<GameState>>()
      .mockResolvedValue(state('9007199254740992')),
  )
  const release = sync.subscribe(gameId, vi.fn())
  await flush()
  getState.mockResolvedValue(state('9007199254740993'))
  events[0]!.emit('9007199254740993')
  await flush()
  expect(sync.getSnapshot(gameId).state?.version).toBe('9007199254740993')
  release()
})

test('bursts during a pending read coalesce and never roll state back', async () => {
  let resolve!: (value: GameState) => void
  const getState = vi
    .fn<() => Promise<GameState>>()
    .mockImplementationOnce(
      () =>
        new Promise((finish) => {
          resolve = finish
        }),
    )
    .mockResolvedValue(state('4'))
  const { sync, events } = setup(getState)
  const release = sync.subscribe(gameId, vi.fn())
  for (let i = 0; i < 20; i++) events[0]!.emit('3')
  expect(getState).toHaveBeenCalledTimes(1)
  resolve(state('2'))
  await flush()
  expect(getState).toHaveBeenCalledTimes(2)
  expect(sync.getSnapshot(gameId).state?.version).toBe('4')
  getState.mockResolvedValue(state('1'))
  await sync.refresh(gameId)
  expect(sync.getSnapshot(gameId).state?.version).toBe('4')
  release()
})
