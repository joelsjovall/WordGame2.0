import { apiClient } from './client.ts'
import { createGuestsApi, guestsApi } from './guests.ts'
import type { components } from './generated.ts'

type CreateLobby = components['schemas']['CreateLobbyRequest']
type JoinLobby = components['schemas']['JoinLobbyRequest']
export type Lobby = components['schemas']['LobbyResponse']
export type LobbySettings = components['schemas']['LobbySettingsRequest']

export function createLobbiesApi(
  client = apiClient,
  identity = createGuestsApi(client),
) {
  return {
    setReady(
      id: string,
      expectedVersion: string,
      isReady: boolean,
    ): Promise<Lobby> {
      return client.request(`/api/v1/lobbies/${encodeURIComponent(id)}/ready`, {
        method: 'POST',
        credentials: 'include',
        body: { expectedVersion, isReady },
      })
    },
    leave(id: string): Promise<Lobby> {
      return client.request(`/api/v1/lobbies/${encodeURIComponent(id)}/leave`, {
        method: 'POST',
        credentials: 'include',
        body: {},
      })
    },
    remove(id: string, playerId: string): Promise<Lobby> {
      return client.request(
        `/api/v1/lobbies/${encodeURIComponent(id)}/remove`,
        { method: 'POST', credentials: 'include', body: { playerId } },
      )
    },
    get(id: string, signal?: AbortSignal): Promise<Lobby> {
      return client.request(`/api/v1/lobbies/${encodeURIComponent(id)}`, {
        credentials: 'include',
        ...(signal ? { signal } : {}),
      })
    },
    configure(
      id: string,
      expectedVersion: string,
      settings: LobbySettings,
    ): Promise<Lobby> {
      return client.request(
        `/api/v1/lobbies/${encodeURIComponent(id)}/settings`,
        {
          method: 'POST',
          credentials: 'include',
          body: { expectedVersion, settings },
        },
      )
    },
    start(id: string, expectedVersion: string): Promise<Lobby> {
      return client.request(`/api/v1/lobbies/${encodeURIComponent(id)}/start`, {
        method: 'POST',
        credentials: 'include',
        body: { expectedVersion },
      })
    },
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
