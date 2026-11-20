import { apiClient } from './client.ts'
import type { components } from './generated.ts'

export type MatchmakingState = components['schemas']['MatchmakingResponse']

export function createMatchmakingApi(client = apiClient) {
  const path = (id: string) =>
    `/api/v1/matchmaking/searches/${encodeURIComponent(id)}`
  return {
    status(signal?: AbortSignal): Promise<MatchmakingState> {
      return client.request('/api/v1/matchmaking/status', {
        credentials: 'include',
        cache: 'no-store',
        ...(signal ? { signal } : {}),
      })
    },
    join(displayName: string): Promise<MatchmakingState> {
      return client.request('/api/v1/matchmaking/searches', {
        method: 'POST',
        credentials: 'include',
        body: { displayName },
      })
    },
    cancel(id: string): Promise<void> {
      return client.request(path(id), {
        method: 'DELETE',
        credentials: 'include',
      })
    },
    respond(
      id: string,
      action: 'accept' | 'decline',
    ): Promise<MatchmakingState> {
      return client.request(`${path(id)}/${action}`, {
        method: 'POST',
        credentials: 'include',
        body: {},
      })
    },
  }
}
export const matchmakingApi = createMatchmakingApi()
