import { expect, test } from '@playwright/test'
import { backendUrl } from './support/environment.ts'

test('host controls synchronize across players and freeze into a valid match', async ({
  page,
  browser,
}) => {
  await page.goto('/')
  await page.getByRole('link', { name: 'Play with Friends' }).click()
  await page.getByLabel('Your display name').fill('Host')
  await page.getByRole('button', { name: 'Create lobby', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Private lobby', exact: true }),
  ).toBeVisible()
  await expect(page.getByRole('button', { name: 'Start match' })).toBeDisabled()
  const invitation = await page.getByLabel('Invitation link').inputValue()
  const roomUrl = page.url()
  const context = await browser.newContext({
    viewport: page.viewportSize() ?? { width: 390, height: 844 },
  })
  try {
    const guest = await context.newPage()
    await guest.goto(invitation)
    await guest.getByLabel('Your display name').fill('Bob')
    await guest.getByRole('button', { name: 'Join lobby' }).click()
    await guest.getByRole('link', { name: 'Open lobby settings' }).click()
    await expect(
      guest.getByRole('heading', { name: 'Private lobby', exact: true }),
    ).toBeVisible()
    await expect(page.getByText('2 of 4 players')).toBeVisible()
    await expect(
      guest.getByRole('button', { name: 'Save settings' }),
    ).toHaveCount(0)
    await page.getByLabel('Maximum players').fill('2')
    await page.getByLabel('Total rounds').fill('7')
    await page.getByLabel('Challenge time (seconds)').fill('75')
    await page.getByLabel('easy', { exact: true }).uncheck()
    await page.getByLabel('medium', { exact: true }).uncheck()
    await page.getByRole('button', { name: 'Save settings' }).click()
    await expect(guest.getByText('Challenge time: 75 seconds')).toBeVisible()
    await expect(guest.getByText('Difficulties: hard')).toBeVisible()
    const lobbyId = roomUrl.split('/').at(-1)!
    const read = await guest.request.get(
      `${backendUrl}/api/v1/lobbies/${lobbyId}`,
    )
    const current = (await read.json()) as { version: string }
    const denied = await guest.request.post(
      `${backendUrl}/api/v1/lobbies/${lobbyId}/start`,
      { data: { expectedVersion: current.version } },
    )
    expect(denied.status()).toBe(403)
    await page.getByRole('button', { name: 'Ready', exact: true }).click()
    await expect(
      guest
        .getByLabel('Lobby players')
        .getByRole('listitem')
        .filter({ hasText: 'Host (host)' }),
    ).toContainText('Connected; Ready')
    await guest.getByRole('button', { name: 'Ready', exact: true }).click()
    await page.getByRole('button', { name: 'Start match' }).click()
    await expect(
      page.getByRole('heading', { name: 'Match started' }),
    ).toBeVisible()
    await expect(
      guest.getByRole('heading', { name: 'Match started' }),
    ).toBeVisible()
    const response = await page.request.get(
      `${backendUrl}/api/v1/lobbies/${lobbyId}`,
    )
    const started = (await response.json()) as {
      gameId: string
      version: string
    }
    const state = await guest.request.get(
      `${backendUrl}/api/v1/games/${started.gameId}/state`,
    )
    expect(state.status()).toBe(200)
    expect(((await state.json()) as { match: unknown }).match).toMatchObject({
      roundCount: 7,
      challengeSeconds: 75,
      allowedDifficulties: ['hard'],
    })
    await page.reload()
    await expect(
      page.getByRole('heading', { name: 'Match started' }),
    ).toBeVisible()
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true)
  } finally {
    await context.close()
  }
})
