import { ApiError } from './api/client.ts'
import type { GameState, gamesApi } from './api/games.ts'
import { createRecoveryLoop } from './recoveryLoop.ts'

export type GameSynchronizationSnapshot = {
  state: GameState | null
  connection: 'connecting' | 'connected' | 'recovering' | 'stopped' | 'error'
  error: Error | null
  receivedAt: number
}

export const emptyGameSnapshot: GameSynchronizationSnapshot = {
  state: null,
  connection: 'connecting',
  error: null,
  receivedAt: 0,
}
export type SynchronizationDependencies = {
  getState: typeof gamesApi.getState
  openEvents: (url: string) => EventSource
  now: () => number
  baseUrl: string
}

export function createGameConnection(
  id: string,
  dependencies: SynchronizationDependencies,
) {
  let snapshot = emptyGameSnapshot
  const listeners = new Set<() => void>()
  const controller = new AbortController()
  let source: EventSource | null = null
  let inFlight: Promise<void> | undefined
  let refreshAgain = false
  let closed = false
  const recovery = createRecoveryLoop(async () => {
    if (closed) return true
    if (!source || source.readyState === 2) open()
    await refresh()
    return closed || snapshot.connection === 'connected'
  })

  function publish(changes: Partial<GameSynchronizationSnapshot>) {
    if (closed) return
    snapshot = { ...snapshot, ...changes }
    for (const listener of listeners) listener()
  }
  function clearRecovery() {
    recovery.reset()
  }
  function stop(changes?: Partial<GameSynchronizationSnapshot>) {
    if (changes) publish(changes)
    closed = true
    recovery.stop()
    source?.close()
    source = null
    controller.abort()
    if (typeof window !== 'undefined') {
      window.removeEventListener('online', online)
      window.removeEventListener('offline', offline)
    }
  }
  function online() {
    if (closed) return
    clearRecovery()
    if (!source || source.readyState !== 1) open()
    void refresh()
  }
  function offline() {
    if (closed) return
    source?.close()
    source = null
    publish({ connection: 'recovering' })
    recover()
  }
  function recover() {
    if (!closed) recovery.schedule()
  }
  function refresh(): Promise<void> {
    if (closed) return Promise.resolve()
    if (inFlight) {
      refreshAgain = true
      return inFlight
    }
    inFlight = dependencies
      .getState(id, controller.signal)
      .then((state) => {
        if (closed || state.id.toLowerCase() !== id) return
        if (
          snapshot.state &&
          BigInt(state.version) < BigInt(snapshot.state.version)
        )
          return
        publish({ state, receivedAt: dependencies.now(), error: null })
        if (source?.readyState === 1) {
          clearRecovery()
          publish({ connection: 'connected' })
        }
      })
      .catch((error: unknown) => {
        if (closed) return
        const failure =
          error instanceof Error
            ? error
            : new Error('Game synchronization failed.')
        if (
          error instanceof ApiError &&
          [401, 403, 404].includes(error.status)
        ) {
          stop({ state: null, connection: 'error', error: failure })
        } else {
          publish({ connection: 'recovering', error: failure })
          recover()
        }
      })
      .finally(() => {
        inFlight = undefined
        if (refreshAgain && !closed) {
          refreshAgain = false
          void refresh()
        }
      })
    return inFlight
  }
  function open() {
    if (closed) return
    source?.close()
    try {
      const current = dependencies.openEvents(
        `${dependencies.baseUrl}/api/v1/games/${encodeURIComponent(id)}/events`,
      )
      source = current
      current.onopen = () => {
        if (closed || source !== current) return
        clearRecovery()
        publish({ connection: 'connected', error: null })
        void refresh()
      }
      current.onerror = () => {
        if (closed || source !== current) return
        publish({ connection: 'recovering' })
        void refresh()
        recover()
      }
      current.addEventListener('game', (event) => {
        if (closed || source !== current) return
        let change: { gameId?: unknown; version?: unknown; kind?: unknown }
        try {
          change = JSON.parse(event.data) as typeof change
        } catch {
          return
        }
        if (
          !change ||
          change.gameId !== id ||
          typeof change.version !== 'string' ||
          !/^(0|[1-9]\d*)$/.test(change.version)
        )
          return
        if (change.kind === 'removed') {
          stop({ state: null, connection: 'stopped', error: null })
        } else if (
          change.kind === 'updated' &&
          (!snapshot.state ||
            BigInt(change.version) > BigInt(snapshot.state.version))
        ) {
          void refresh()
        }
      })
    } catch {
      source = null
      publish({ connection: 'recovering' })
      recover()
    }
  }
  return {
    listeners,
    start: () => {
      if (typeof window !== 'undefined') {
        window.addEventListener('online', online)
        window.addEventListener('offline', offline)
      }
      open()
      void refresh()
    },
    stop,
    refresh,
    snapshot: () => snapshot,
  }
}
