import { expect, test, vi } from 'vitest'
import { ApiError, createApiClient } from '../src/services/api/client.ts'
import { createGuestsApi } from '../src/services/api/guests.ts'
import { createLobbiesApi } from '../src/services/api/lobbies.ts'

const request = {
  requestId: 'e24d1b2e-9fb5-4076-b018-c04ab02134c6',
  displayName: 'Alice',
}
const guest = { playerId: '7ede379e-1770-450e-b5e6-a6042156bb2c' }

test('creation bootstraps a guest and sends the stable request ID with credentials', async () => {
  const calls: { url: string; options: RequestInit | undefined }[] = []
  const controller = new AbortController()
  const client = createApiClient(
    'https://api.example',
    async (input, options) => {
      calls.push({ url: String(input), options })
      return Response.json(
        String(input).endsWith('/guests') ? guest : { id: 'lobby-id' },
      )
    },
  )
  expect(
    (await createLobbiesApi(client).create(request, controller.signal)).id,
  ).toBe('lobby-id')
  expect(calls.map((call) => call.url)).toEqual([
    'https://api.example/api/v1/guests',
    'https://api.example/api/v1/lobbies',
  ])
  for (const call of calls) {
    expect(call.options?.method).toBe('POST')
    expect(call.options?.credentials).toBe('include')
    expect(new Headers(call.options?.headers).get('Content-Type')).toBe(
      'application/json',
    )
  }
  expect(JSON.parse(String(calls[0]!.options?.body))).toEqual({})
  expect(JSON.parse(String(calls[1]!.options?.body))).toEqual(request)
  expect(calls[1]!.options?.signal).toBe(controller.signal)
})

test('parallel creations share guest bootstrap without caching identity indefinitely', async () => {
  let release!: (response: Response) => void
  let guestCalls = 0
  let lobbyCalls = 0
  const client = createApiClient('', async (input) => {
    if (String(input).endsWith('/guests')) {
      guestCalls++
      if (guestCalls === 1)
        return new Promise<Response>((resolve) => {
          release = resolve
        })
      return Response.json(guest)
    }
    lobbyCalls++
    return Response.json({ id: 'lobby-id' })
  })
  const lobbies = createLobbiesApi(client)
  const first = lobbies.create(request)
  const second = lobbies.create(request)
  expect(guestCalls).toBe(1)
  release(Response.json(guest))
  await Promise.all([first, second])
  expect(lobbyCalls).toBe(2)
  await lobbies.create(request)
  expect(guestCalls).toBe(2)
})

test('bootstrap failures create no lobby and can be retried explicitly', async () => {
  let fail = true
  const fetchRequest = vi.fn(async (input: RequestInfo | URL) => {
    if (String(input).endsWith('/guests'))
      return fail ? Response.json({}, { status: 503 }) : Response.json(guest)
    return Response.json({ id: 'lobby-id' })
  })
  const lobbies = createLobbiesApi(createApiClient('', fetchRequest))
  await expect(lobbies.create(request)).rejects.toMatchObject({ status: 503 })
  expect(fetchRequest).toHaveBeenCalledTimes(1)
  fail = false
  expect((await lobbies.create(request)).id).toBe('lobby-id')
  expect(fetchRequest).toHaveBeenCalledTimes(3)
})

test('cancelling one caller does not cancel a shared bootstrap or create its lobby', async () => {
  let release!: (response: Response) => void
  let created = 0
  const client = createApiClient('', async (input) => {
    if (String(input).endsWith('/guests'))
      return new Promise<Response>((resolve) => {
        release = resolve
      })
    created++
    return Response.json({ id: 'lobby-id' })
  })
  const identity = createGuestsApi(client)
  const lobbies = createLobbiesApi(client, identity)
  const controller = new AbortController()
  const cancelled = lobbies.create(request, controller.signal)
  const rejection = expect(cancelled).rejects.toMatchObject({
    name: 'AbortError',
  })
  const active = lobbies.create(request)
  controller.abort()
  release(Response.json(guest))
  await rejection
  await active
  expect(created).toBe(1)
})

test('server conflicts propagate without an automatic creation retry', async () => {
  let calls = 0
  const client = createApiClient('', async (input) => {
    calls++
    return String(input).endsWith('/guests')
      ? Response.json(guest)
      : Response.json({ title: 'Request ID conflict.' }, { status: 409 })
  })
  await expect(createLobbiesApi(client).create(request)).rejects.toBeInstanceOf(
    ApiError,
  )
  expect(calls).toBe(2)
})
