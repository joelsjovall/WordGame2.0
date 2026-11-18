import { useCallback, useSyncExternalStore } from 'react'
import { gameSynchronization } from '../../services/gameSynchronization.ts'

export function useGameState(
  gameId: string,
  synchronization = gameSynchronization,
) {
  const subscribe = useCallback(
    (listener: () => void) => synchronization.subscribe(gameId, listener),
    [gameId, synchronization],
  )
  const snapshot = useCallback(
    () => synchronization.getSnapshot(gameId),
    [gameId, synchronization],
  )
  return useSyncExternalStore(subscribe, snapshot, snapshot)
}
