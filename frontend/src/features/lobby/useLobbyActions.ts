import { useRef, useState } from 'react'
import { apiErrorMessage } from '../../services/api/client.ts'
import { lobbiesApi, type Lobby } from '../../services/api/lobbies.ts'

export function useLobbyActions(id: string, accept: (lobby: Lobby) => void) {
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  async function act(operation: () => Promise<Lobby>, after?: () => void) {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      accept(await operation())
      after?.()
    } catch (failure) {
      setError(apiErrorMessage(failure))
      try {
        accept(await lobbiesApi.get(id))
      } catch {
        /* Preserve the action error. */
      }
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return { act, error, busy }
}
