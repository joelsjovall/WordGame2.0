import { expect, test, vi } from 'vitest'
import { createApiClient } from '../src/services/api/client.ts'
import { createGuestsApi } from '../src/services/api/guests.ts'

test('reset waits for bootstrap and later bootstraps wait for reset without sending player IDs', async () => {
  let completeBootstrap!: (value: Response) => void
  let completeReset!: (value: Response) => void
  const requests: string[] = []
  const fetcher = vi.fn((input: RequestInfo | URL, options?: RequestInit) => {
    requests.push(String(input))
    expect(options?.credentials).toBe('include')
    expect(options?.body).toBe('{}')
    if (requests.length === 1)
      return new Promise<Response>((resolve) => {
        completeBootstrap = resolve
      })
    if (requests.length === 2)
      return new Promise<Response>((resolve) => {
        completeReset = resolve
      })
    return Promise.resolve(Response.json({ playerId: 'new' }))
  })
  const guests = createGuestsApi(createApiClient('', fetcher))
  const initial = guests.ensure()
  expect(guests.ensure()).toBe(initial)
  const reset = guests.reset()
  expect(guests.reset()).toBe(reset)
  const later = guests.ensure()
  expect(requests).toHaveLength(1)
  completeBootstrap(Response.json({ playerId: 'old' }))
  await initial
  await vi.waitFor(() => expect(requests).toHaveLength(2))
  completeReset(new Response(null, { status: 204 }))
  await reset
  await expect(later).resolves.toEqual({ playerId: 'new' })
  expect(requests).toEqual([
    '/api/v1/guests',
    '/api/v1/guests/reset',
    '/api/v1/guests',
  ])
})
