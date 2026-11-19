import { apiClient } from './client.ts'
import type { components } from './generated.ts'

export type GameState = components['schemas']['GameStateResponse']
export type GameAction = components['schemas']['GameActionRequest']

export function createGamesApi(client = apiClient) {
  return {
    act(
      gameId: string,
      action: GameAction,
    ): Promise<components['schemas']['GameActionResponse']> {
      return client.request(
        `/api/v1/games/${encodeURIComponent(gameId)}/actions`,
        { method: 'POST', credentials: 'include', body: action },
      )
    },
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
