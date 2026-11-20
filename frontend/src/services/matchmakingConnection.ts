import { ApiError } from './api/client.ts'
import { apiBaseUrl } from './api/config.ts'
import { gamesApi } from './api/games.ts'
import { guestsApi } from './api/guests.ts'
import { matchmakingApi, type MatchmakingState } from './api/matchmaking.ts'
import { createRecoveryLoop } from './recoveryLoop.ts'

export type MatchmakingSnapshot = {
  state: MatchmakingState | null
  playerId: string
  opponent: string
  connection: 'connecting' | 'connected' | 'recovering' | 'stopped'
  busy: boolean
  error: string
  actionError: string
}
export const emptyMatchmaking: MatchmakingSnapshot = {
  state: null,
  playerId: '',
  opponent: '',
  connection: 'connecting',
  busy: false,
  error: '',
  actionError: '',
}

function readState(value: unknown): MatchmakingState {
  const state = value as MatchmakingState | null
  if (
    !state ||
    ![
      'idle',
      'searching',
      'matched',
      'ready',
      'failed',
      'cancelled',
      'expired',
    ].includes(state.status) ||
    (state.status !== 'idle' && typeof state.searchId !== 'string') ||
    (['matched', 'ready'].includes(state.status) &&
      typeof state.gameId !== 'string') ||
    (state.expiresAt !== null && !Number.isFinite(Date.parse(state.expiresAt)))
  ) {
    throw new Error('Invalid matchmaking update.')
  }
  return state
}

export function connectMatchmaking(
  listener: (value: MatchmakingSnapshot) => void,
) {
  let snapshot = emptyMatchmaking
  let source: EventSource | undefined
  let stopped = false
  let sequence = 0
  let previewGame = ''
  let refreshing: Promise<void> | undefined
  let initializing = false
  const abort = new AbortController()
  const publish = (changes: Partial<MatchmakingSnapshot>) => {
    if (!stopped) {
      snapshot = { ...snapshot, ...changes }
      listener(snapshot)
    }
  }
  const recovery = createRecoveryLoop(async () => {
    await initialize()
    return stopped || source?.readyState === 1
  })
  function failure(error: unknown) {
    if (stopped) return
    if (error instanceof ApiError && [401, 403].includes(error.status)) {
      publish({
        connection: 'stopped',
        busy: false,
        error:
          error.status === 401
            ? 'Your guest session is no longer available. Return home to start again.'
            : error.message,
        actionError: '',
      })
      stopped = true
      source?.close()
      recovery.stop()
      abort.abort()
    } else {
      publish({
        connection: 'recovering',
        error:
          error instanceof ApiError
            ? error.message
            : 'Connection interrupted. Reconnecting…',
      })
      recovery.schedule()
    }
  }
  function accept(value: unknown) {
    const state = readState(value)
    if (JSON.stringify(state) !== JSON.stringify(snapshot.state)) sequence++
    publish({ state, error: '' })
    if (state.gameId && state.gameId !== previewGame) {
      previewGame = state.gameId
      publish({ opponent: '' })
      const id = state.gameId
      void gamesApi
        .getState(id, abort.signal)
        .then((game) => {
          if (!stopped && snapshot.state?.gameId === id) {
            publish({
              opponent:
                game.players.find((player) => player.id !== snapshot.playerId)
                  ?.displayName ?? '',
            })
          }
        })
        .catch(() => {
          if (previewGame === id) previewGame = ''
        })
    }
  }
  function refresh(): Promise<void> {
    if (stopped) return Promise.resolve()
    refreshing ??= (async () => {
      const before = sequence
      try {
        const state = await matchmakingApi.status(abort.signal)
        if (!stopped && sequence === before) {
          accept(state)
          if (source?.readyState === 1) publish({ connection: 'connected' })
        }
      } catch (error) {
        failure(error)
      }
    })().finally(() => {
      refreshing = undefined
    })
    return refreshing
  }
  function open() {
    source?.close()
    try {
      const events = new EventSource(
        `${apiBaseUrl}/api/v1/matchmaking/events`,
        { withCredentials: true },
      )
      source = events
      const current = () => !stopped && source === events
      events.onopen = () => {
        if (!current()) return
        publish({ connection: 'connected', error: '' })
        recovery.reset()
        void refresh()
      }
      events.addEventListener('matchmaking', (event) => {
        if (!current()) return
        try {
          accept(JSON.parse((event as MessageEvent<string>).data))
          publish({ connection: 'connected' })
          recovery.reset()
        } catch {
          failure(new Error('Invalid event'))
          void refresh()
        }
      })
      events.onerror = () => {
        if (!current()) return
        publish({
          connection: 'recovering',
          error: 'Connection interrupted. Reconnecting…',
        })
        void refresh()
        recovery.schedule()
      }
    } catch (error) {
      source = undefined
      failure(error)
    }
  }
  async function initialize() {
    if (initializing || stopped) return
    initializing = true
    try {
      if (!snapshot.playerId) {
        const identity = await guestsApi.ensure()
        if (stopped) return
        publish({ playerId: identity.playerId })
      }
      await refresh()
      if (!stopped && (!source || source.readyState === 2)) open()
    } catch (error) {
      failure(error)
    } finally {
      initializing = false
    }
  }
  async function act(operation: () => Promise<MatchmakingState | void>) {
    if (stopped || snapshot.busy || snapshot.connection !== 'connected') return
    publish({ busy: true, actionError: '' })
    const before = sequence
    try {
      const state = await operation()
      if (!stopped && state && sequence === before) accept(state)
      if (!state) await refresh()
    } catch (error) {
      if (error instanceof ApiError && [401, 403].includes(error.status))
        failure(error)
      else {
        publish({
          actionError:
            error instanceof ApiError
              ? error.message
              : 'Unable to send your request. Check your connection and try again.',
        })
        await refresh()
      }
    } finally {
      publish({ busy: false })
    }
  }
  function online() {
    if (!stopped) {
      recovery.reset()
      void initialize()
    }
  }
  function offline() {
    if (stopped) return
    source?.close()
    source = undefined
    publish({
      connection: 'recovering',
      error:
        'You are offline. Your search will be restored when the connection returns.',
    })
    recovery.schedule()
  }
  window.addEventListener('online', online)
  window.addEventListener('offline', offline)
  void initialize()
  return {
    join: (name: string) => act(() => matchmakingApi.join(name)),
    cancel: () =>
      act(async () => {
        if (snapshot.state?.searchId)
          await matchmakingApi.cancel(snapshot.state.searchId)
      }),
    respond: (action: 'accept' | 'decline') =>
      act(async () => {
        if (snapshot.state?.searchId)
          return matchmakingApi.respond(snapshot.state.searchId, action)
      }),
    stop: () => {
      stopped = true
      abort.abort()
      source?.close()
      recovery.stop()
      window.removeEventListener('online', online)
      window.removeEventListener('offline', offline)
    },
  }
}
