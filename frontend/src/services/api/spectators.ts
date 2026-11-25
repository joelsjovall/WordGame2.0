import { apiClient } from './client.ts'
import type { components } from './generated.ts'

export type SpectatorState = components['schemas']['SpectatorStateResponse']

export const spectatorsApi = {
  get(lobbyId: string, code: string, signal?: AbortSignal) {
    return apiClient.request<SpectatorState>(
      `/api/v1/lobbies/${encodeURIComponent(lobbyId)}/spectate/${encodeURIComponent(code)}`,
      { cache: 'no-store', ...(signal ? { signal } : {}) },
    )
  },
}
