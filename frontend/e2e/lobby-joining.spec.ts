import { randomUUID } from 'node:crypto'
import { expect, test, type APIRequestContext } from '@playwright/test'
import type { components } from '../src/services/api/generated.ts'
import { backendUrl, frontendUrl } from './support/environment.ts'

async function createRoom(request: APIRequestContext) {
  expect(
    (await request.post(backendUrl + '/api/v1/guests', { data: {} })).status(),
  ).toBe(200)
  const response = await request.post(backendUrl + '/api/v1/lobbies', {
    data: { requestId: randomUUID(), displayName: 'Host' },
  })
  expect(response.status()).toBe(201)
  return (await response.json()) as components['schemas']['LobbyResponse']
}

test('a new guest joins by code from the homepage on desktop or mobile', async ({
  page,
  request,
}) => {
  const lobby = await createRoom(request)
  await page.goto('/')
  await page.getByRole('link', { name: 'Join a private lobby' }).click()
  await page.getByLabel('Lobby code').fill(lobby.lobbyCode.toLowerCase())
  await page.getByLabel('Your display name').fill('Bob')
  await page.getByRole('button', { name: 'Join lobby' }).click()
  await expect(
    page.getByRole('heading', { name: 'You joined the lobby' }),
  ).toBeVisible()
  await expect(page.getByText('2 of 4 players')).toBeVisible()
  await expect(page.getByLabel('Lobby players')).toContainText('Host (host)')
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true)
})

test('direct invitations support copying, native-share capability, and safe rejoining after reload', async ({
  page,
  request,
}) => {
  const lobby = await createRoom(request)
  const invitation = `${frontendUrl}/join/${lobby.id}/${lobby.lobbyCode}`
  await page.addInitScript(() => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: async (text: string) => {
          document.documentElement.dataset.copiedInvitation = text
        },
      },
    })
    Object.defineProperty(navigator, 'share', {
      configurable: true,
      value: async (data: ShareData) => {
        document.documentElement.dataset.sharedInvitation = data.url
      },
    })
  })
  await page.goto(invitation)
  await expect(page.getByLabel('Lobby code')).toHaveValue(lobby.lobbyCode)
  await expect(page.getByLabel('Lobby code')).toHaveAttribute('readonly')
  await page.getByLabel('Your display name').fill('Bob')
  await page.getByRole('button', { name: 'Join lobby' }).click()
  await expect(
    page.getByRole('heading', { name: 'You joined the lobby' }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Copy invitation link' }).click()
  await expect(page.locator('html')).toHaveAttribute(
    'data-copied-invitation',
    invitation,
  )
  await page.getByRole('button', { name: 'Share invitation' }).click()
  await expect(page.locator('html')).toHaveAttribute(
    'data-shared-invitation',
    invitation,
  )
  await page.reload()
  await page.getByLabel('Your display name').fill('bob')
  await page.getByRole('button', { name: 'Join lobby' }).click()
  await expect(page.getByText('2 of 4 players')).toBeVisible()
})

test('name conflicts are clear and can be corrected without account registration', async ({
  page,
  request,
}) => {
  const lobby = await createRoom(request)
  await page.goto(`${frontendUrl}/join/${lobby.id}/${lobby.lobbyCode}`)
  await page.getByLabel('Your display name').fill('HOST')
  await page.getByRole('button', { name: 'Join lobby' }).click()
  await expect(page.getByRole('alert')).toHaveText(
    'That display name is already in use in this lobby.',
  )
  await page.getByLabel('Your display name').fill('Bob')
  await page.getByRole('button', { name: 'Join lobby' }).click()
  await expect(
    page.getByRole('heading', { name: 'You joined the lobby' }),
  ).toBeVisible()
})
