import { apiClient } from './client.ts'
import { createGuestsApi, guestsApi } from './guests.ts'
import type { components } from './generated.ts'

type CreateLobby = components['schemas']['CreateLobbyRequest']
type JoinLobby = components['schemas']['JoinLobbyRequest']
type Lobby = components['schemas']['LobbyResponse']

export function createLobbiesApi(
  client = apiClient,
  identity = createGuestsApi(client),
) {
  return {
    async join(request: JoinLobby, signal?: AbortSignal): Promise<Lobby> {
      await identity.ensure()
      signal?.throwIfAborted()
      return client.request('/api/v1/lobbies/join', {
        method: 'POST',
        credentials: 'include',
        body: request,
        ...(signal ? { signal } : {}),
      })
    },
    async create(request: CreateLobby, signal?: AbortSignal): Promise<Lobby> {
      await identity.ensure()
      signal?.throwIfAborted()
      return client.request('/api/v1/lobbies', {
        method: 'POST',
        credentials: 'include',
        body: request,
        ...(signal ? { signal } : {}),
      })
    },
  }
}

export const lobbiesApi = createLobbiesApi(apiClient, guestsApi)
