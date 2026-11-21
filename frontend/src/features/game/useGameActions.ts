import { useRef, useState } from 'react'
import { gamesApi } from '../../services/api/games.ts'
import { ApiError } from '../../services/api/client.ts'
import { gameSynchronization } from '../../services/gameSynchronization.ts'
import type { GameCommand } from './gameTypes.ts'

export function useGameActions(
  id: string,
  version: string | undefined,
  canAct: boolean,
) {
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  async function send(command: GameCommand) {
    if (!version || !canAct || pending.current) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      await gamesApi.act(id, {
        ...command,
        actionId: crypto.randomUUID(),
        expectedVersion: version,
      })
      await gameSynchronization.refresh(id)
    } catch (failure) {
      setError(
        failure instanceof ApiError
          ? failure.message
          : 'Unable to submit the action. Please try again.',
      )
      // Refresh failures have their own synchronization state; retain the original action error.
      await gameSynchronization.refresh(id).catch(() => {})
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  return { error, busy, send }
}
