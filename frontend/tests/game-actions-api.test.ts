import { expect, test } from 'vitest'
import { createApiClient } from '../src/services/api/client.ts'
import { createGamesApi } from '../src/services/api/games.ts'
import { createLobbiesApi } from '../src/services/api/lobbies.ts'

test('game commands and rematch preserve command versions and use protected cookies', async () => {
  const calls: { url: string; options: RequestInit | undefined }[] = []
  const client = createApiClient('', async (input, options) => {
    calls.push({ url: String(input), options })
    return Response.json({ version: '9' })
  })
  const command = {
    type: 'placeBid' as const,
    actionId: 'action',
    expectedVersion: '8',
    wordCount: 2,
    risk: false,
  }
  await createGamesApi(client).act('game', command)
  await createLobbiesApi(client).rematch('room', '10')
  expect(calls.map((call) => call.url)).toEqual([
    '/api/v1/games/game/actions',
    '/api/v1/lobbies/room/rematch',
  ])
  expect(calls.map((call) => JSON.parse(String(call.options?.body)))).toEqual([
    command,
    { expectedVersion: '10' },
  ])
  expect(
    calls.every(
      (call) =>
        call.options?.credentials === 'include' &&
        call.options.method === 'POST',
    ),
  ).toBe(true)
})
