import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../services/api/client.ts'
import { apiBaseUrl } from '../../services/api/config.ts'
import { guestsApi } from '../../services/api/guests.ts'
import { lobbiesApi, type Lobby } from '../../services/api/lobbies.ts'

export function useLobby(id: string) {
  const [lobby, setLobby] = useState<Lobby | null>(null)
  const [playerId, setPlayerId] = useState('')
  const [error, setError] = useState('')
  const [unavailable, setUnavailable] = useState(false)
  const [connection, setConnection] = useState('Connecting…')
  const accept = useCallback((next: Lobby) => {
    if (!/^(0|[1-9]\d*)$/.test(next.version))
      throw new Error('Invalid lobby revision.')
    setLobby((previous) =>
      !previous ||
      previous.id !== next.id ||
      BigInt(next.version) >= BigInt(previous.version)
        ? next
        : previous,
    )
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    let events: EventSource | undefined
    let refreshing = false
    let terminal = false
    async function refresh() {
      if (refreshing || controller.signal.aborted) return
      refreshing = true
      try {
        const state = await lobbiesApi.get(id, controller.signal)
        if (!controller.signal.aborted && !terminal) {
          accept(state)
          setError('')
        }
      } catch (failure) {
        if (!controller.signal.aborted && !terminal) {
          setError(
            failure instanceof ApiError && failure.status === 404
              ? 'This lobby is no longer available. Ask the host for a new invitation.'
              : failure instanceof ApiError
                ? failure.message
                : 'Unable to load the lobby. Check your connection.',
          )
          if (
            failure instanceof ApiError &&
            [401, 403, 404].includes(failure.status)
          ) {
            terminal = true
            setUnavailable(true)
            events?.close()
          }
        }
      } finally {
        refreshing = false
      }
    }
    void guestsApi
      .ensure()
      .then(async (guest) => {
        if (controller.signal.aborted) return
        setPlayerId(guest.playerId)
        await refresh()
        if (controller.signal.aborted || terminal) return
        events = new EventSource(
          `${apiBaseUrl}/api/v1/lobbies/${encodeURIComponent(id)}/events`,
          { withCredentials: true },
        )
        events.addEventListener('lobby', (event) => {
          if (controller.signal.aborted || terminal) return
          try {
            const update = JSON.parse(
              (event as MessageEvent<string>).data,
            ) as Lobby
            if (update.id !== id) throw new Error('Unexpected lobby.')
            accept(update)
            setConnection('Live')
            setError('')
          } catch {
            setError('Unable to read a lobby update.')
            void refresh()
          }
        })
        events.addEventListener('expired', () => {
          if (controller.signal.aborted) return
          terminal = true
          setUnavailable(true)
          events?.close()
          setError('This lobby is no longer available.')
        })
        events.addEventListener('removed', () => {
          if (controller.signal.aborted) return
          terminal = true
          events?.close()
          setUnavailable(true)
          setError('You have left or been removed from this lobby.')
        })
        events.onerror = () => {
          if (!controller.signal.aborted && !terminal) {
            setConnection('Reconnecting…')
            void refresh()
          }
        }
      })
      .catch(() => {
        if (!controller.signal.aborted)
          setError('Unable to establish your guest session. Please reload.')
      })
    return () => {
      controller.abort()
      events?.close()
    }
  }, [id, accept])
  return { lobby, playerId, error, connection, unavailable, accept }
}
