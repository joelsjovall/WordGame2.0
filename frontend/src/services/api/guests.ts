import { apiClient } from './client.ts'
import type { components } from './generated.ts'

type GuestIdentity = components['schemas']['GuestIdentityResponse']

export function createGuestsApi(client = apiClient) {
  let pending: Promise<GuestIdentity> | undefined
  return {
    ensure(): Promise<GuestIdentity> {
      pending ??= client
        .request<GuestIdentity>('/api/v1/guests', {
          method: 'POST',
          credentials: 'include',
          body: {},
        })
        .finally(() => {
          pending = undefined
        })
      return pending
    },
  }
}

export const guestsApi = createGuestsApi()
