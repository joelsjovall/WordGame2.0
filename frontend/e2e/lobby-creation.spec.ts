import { expect, test } from '@playwright/test'
import { backendUrl } from './support/environment.ts'

test('a browser creates a private lobby without an account and replays its request', async ({
  page,
  context,
}) => {
  await page.goto('/')
  const result = await page.evaluate(async () => {
    const modulePath = '/src/services/api/lobbies.ts'
    const { lobbiesApi } = (await import(
      modulePath
    )) as typeof import('../src/services/api/lobbies.ts')
    const request = { requestId: crypto.randomUUID(), displayName: 'Alice' }
    return {
      created: await lobbiesApi.create(request),
      replayed: await lobbiesApi.create(request),
    }
  })
  expect(result.created.id).toBe(result.replayed.id)
  expect(result.created.lobbyCode).toMatch(
    /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$/,
  )
  expect(result.created.status).toBe('waiting')
  expect(result.created.players).toEqual([
    {
      id: result.created.hostPlayerId,
      displayName: 'Alice',
      isReady: false,
      isConnected: false,
    },
  ])
  expect(result.created.maximumPlayers).toBe(4)
  const cookies = await context.cookies(backendUrl)
  expect(
    cookies.some(
      (cookie) => cookie.name === 'wordgame.player' && cookie.httpOnly,
    ),
  ).toBe(true)
})
