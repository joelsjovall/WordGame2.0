import { apiClient } from './client.ts'
import type { components } from './generated.ts'

type GuestIdentity = components['schemas']['GuestIdentityResponse']

export function createGuestsApi(client = apiClient) {
  let pending: Promise<GuestIdentity> | undefined
  let resetting: Promise<void> | undefined
  function ensure(): Promise<GuestIdentity> {
    if (resetting) return resetting.then(ensure)
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
  }
  function reset(): Promise<void> {
    resetting ??= (async () => {
      // Complete an in-flight bootstrap before clearing its cookie; later bootstraps wait for reset.
      await pending?.catch(() => undefined)
      await client.request<void>('/api/v1/guests/reset', {
        method: 'POST',
        credentials: 'include',
        body: {},
      })
    })().finally(() => {
      resetting = undefined
    })
    return resetting
  }
  return { ensure, reset }
}

export const guestsApi = createGuestsApi()
