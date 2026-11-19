import { ApiError } from './api/client.ts'
import { apiBaseUrl } from './api/config.ts'
import { guestsApi } from './api/guests.ts'
import { lobbiesApi, type Lobby } from './api/lobbies.ts'
import { createRecoveryLoop } from './recoveryLoop.ts'

type Callbacks = {
  state: (value: Lobby) => void
  identity: (id: string) => void
  connection: (value: string) => void
  error: (message: string) => void
  unavailable: () => void
}

export function connectLobby(id: string, callbacks: Callbacks) {
  const controller = new AbortController()
  let events: EventSource | undefined
  let refreshing = false
  let initializing = false
  let identityKnown = false
  let closed = false
  let terminal = false
  const stopped = () => closed || terminal
  const current = (source: EventSource) => !stopped() && events === source
  const recovery = createRecoveryLoop(async () => {
    if (stopped()) return true
    await initialize()
    return stopped() || events?.readyState === 1
  })
  function stopAccess(message: string) {
    terminal = true
    events?.close()
    recovery.stop()
    callbacks.error(message)
    callbacks.unavailable()
  }
  async function refresh() {
    if (refreshing || stopped()) return
    refreshing = true
    try {
      const state = await lobbiesApi.get(id, controller.signal)
      if (!stopped()) {
        callbacks.state(state)
        callbacks.error('')
      }
    } catch (failure) {
      if (!stopped()) {
        const message =
          failure instanceof ApiError && failure.status === 404
            ? 'This lobby is no longer available. Ask the host for a new invitation.'
            : failure instanceof ApiError
              ? failure.message
              : 'Unable to load the lobby. Retrying your connection…'
        if (
          failure instanceof ApiError &&
          [401, 403, 404].includes(failure.status)
        )
          stopAccess(message)
        else {
          callbacks.error(message)
          callbacks.connection('Reconnecting…')
          recovery.schedule()
        }
      }
    } finally {
      refreshing = false
    }
  }
  function open() {
    events?.close()
    try {
      const source = new EventSource(
        `${apiBaseUrl}/api/v1/lobbies/${encodeURIComponent(id)}/events`,
        { withCredentials: true },
      )
      events = source
      source.onopen = () => {
        if (current(source)) {
          callbacks.connection('Live')
          recovery.reset()
          void refresh()
        }
      }
      source.addEventListener('lobby', (event) => {
        if (!current(source)) return
        try {
          const state = JSON.parse(
            (event as MessageEvent<string>).data,
          ) as Lobby
          if (state.id !== id) throw new Error('Unexpected lobby.')
          callbacks.state(state)
          callbacks.error('')
          callbacks.connection('Live')
          recovery.reset()
        } catch {
          callbacks.error('Unable to read a lobby update.')
          void refresh()
        }
      })
      source.addEventListener('expired', () => {
        if (current(source)) stopAccess('This lobby is no longer available.')
      })
      source.addEventListener('removed', () => {
        if (current(source))
          stopAccess('You have left or been removed from this lobby.')
      })
      source.onerror = () => {
        if (current(source)) {
          callbacks.connection('Reconnecting…')
          void refresh()
          recovery.schedule()
        }
      }
    } catch {
      events = undefined
      callbacks.connection('Reconnecting…')
      recovery.schedule()
    }
  }
  async function initialize() {
    if (initializing || stopped()) return
    initializing = true
    try {
      if (!identityKnown) {
        const identity = await guestsApi.ensure()
        if (stopped()) return
        identityKnown = true
        callbacks.identity(identity.playerId)
      }
      await refresh()
      if (!stopped() && (!events || events.readyState === 2)) open()
    } catch {
      if (!stopped()) {
        callbacks.connection('Reconnecting…')
        callbacks.error('Unable to reach your guest session. Retrying…')
        recovery.schedule()
      }
    } finally {
      initializing = false
    }
  }
  function online() {
    if (!stopped()) {
      recovery.reset()
      void initialize()
    }
  }
  function offline() {
    if (stopped()) return
    events?.close()
    events = undefined
    callbacks.connection('Reconnecting…')
    recovery.schedule()
  }
  window.addEventListener('online', online)
  window.addEventListener('offline', offline)
  void initialize()
  return () => {
    closed = true
    controller.abort()
    events?.close()
    recovery.stop()
    window.removeEventListener('online', online)
    window.removeEventListener('offline', offline)
  }
}
