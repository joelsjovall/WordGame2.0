import { apiClient } from './client.ts'
import type { components } from './generated.ts'

export type GameState = components['schemas']['GameStateResponse']

export function createGamesApi(client = apiClient) {
  return {
    getState(gameId: string, signal?: AbortSignal): Promise<GameState> {
      return client.request(
        `/api/v1/games/${encodeURIComponent(gameId)}/state`,
        {
          credentials: 'include',
          cache: 'no-store',
          ...(signal ? { signal } : {}),
        },
      )
    },
  }
}

export const gamesApi = createGamesApi()
