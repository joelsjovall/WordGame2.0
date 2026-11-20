import { apiClient } from './client.ts'
import type { components } from './generated.ts'

export type RematchState = components['schemas']['OnlineRematchResponse']
export type RematchAction = components['schemas']['ApiRematchAction']
const path = (id: string) => `/api/v1/games/${encodeURIComponent(id)}/rematch`
export const rematchesApi = {
  get(id: string, signal?: AbortSignal): Promise<RematchState> {
    return apiClient.request(path(id), {
      credentials: 'include',
      cache: 'no-store',
      ...(signal ? { signal } : {}),
    })
  },
  respond(id: string, action: RematchAction): Promise<RematchState> {
    return apiClient.request(path(id), {
      method: 'POST',
      credentials: 'include',
      body: { action },
    })
  },
}
