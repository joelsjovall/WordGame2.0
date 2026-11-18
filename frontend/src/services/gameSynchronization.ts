import { apiBaseUrl } from './api/config.ts'
import { gamesApi } from './api/games.ts'
import {
  createGameConnection,
  emptyGameSnapshot,
  type SynchronizationDependencies,
} from './gameSynchronizationConnection.ts'
export type { GameSynchronizationSnapshot } from './gameSynchronizationConnection.ts'

export function createGameSynchronization(
  overrides: Partial<SynchronizationDependencies> = {},
) {
  const dependencies: SynchronizationDependencies = {
    getState: gamesApi.getState,
    openEvents: (url) => new EventSource(url, { withCredentials: true }),
    now: () => performance.now(),
    baseUrl: apiBaseUrl,
    ...overrides,
  }
  const entries = new Map<string, ReturnType<typeof createGameConnection>>()
  const key = (id: string) => id.toLowerCase()

  return {
    subscribe(gameId: string, listener: () => void) {
      const id = key(gameId)
      let entry = entries.get(id)
      const first = !entry
      if (!entry) {
        entry = createGameConnection(id, dependencies)
        entries.set(id, entry)
      }
      const subscriptionListener = () => listener()
      entry.listeners.add(subscriptionListener)
      if (first) entry.start()
      const subscribed = entry
      let released = false
      return () => {
        if (released) return
        released = true
        subscribed.listeners.delete(subscriptionListener)
        if (subscribed.listeners.size === 0) {
          subscribed.stop()
          if (entries.get(id) === subscribed) entries.delete(id)
        }
      }
    },
    getSnapshot: (gameId: string) =>
      entries.get(key(gameId))?.snapshot() ?? emptyGameSnapshot,
    refresh: (gameId: string) =>
      entries.get(key(gameId))?.refresh() ?? Promise.resolve(),
    remaining(
      gameId: string,
      deadline: string | null | undefined,
    ): number | null {
      const snapshot = entries.get(key(gameId))?.snapshot()
      if (!deadline || !snapshot?.state) return null
      const end = Date.parse(deadline)
      const serverNow =
        Date.parse(snapshot.state.serverTime) +
        dependencies.now() -
        snapshot.receivedAt
      return Number.isFinite(end) && Number.isFinite(serverNow)
        ? Math.max(0, end - serverNow)
        : null
    },
  }
}

export const gameSynchronization = createGameSynchronization()
