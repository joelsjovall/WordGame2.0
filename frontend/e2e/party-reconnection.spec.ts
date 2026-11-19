import { expect, test, type Browser, type Page } from '@playwright/test'
import type { GameState } from '../src/services/api/games.ts'
import { backendUrl } from './support/environment.ts'

test.setTimeout(45000)
async function startParty(page: Page, browser: Browser) {
  await page.goto('/lobbies/new')
  await page.getByLabel('Your display name').fill('Host')
  await page.getByRole('button', { name: 'Create lobby', exact: true }).click()
  const invitation = await page.getByLabel('Invitation link').inputValue()
  const lobbyId = page.url().split('/').at(-1)!
  const context = await browser.newContext({
    viewport: page.viewportSize() ?? { width: 390, height: 844 },
  })
  const guest = await context.newPage()
  await guest.goto(invitation)
  await guest.getByLabel('Your display name').fill('Bob')
  await guest.getByRole('button', { name: 'Join lobby', exact: true }).click()
  await guest.getByRole('link', { name: 'Open lobby settings' }).click()
  await expect(page.getByText('2 of 4 players')).toBeVisible()
  await page.getByLabel('Total rounds').fill('2')
  await page.getByLabel('medium', { exact: true }).uncheck()
  await page.getByLabel('hard', { exact: true }).uncheck()
  await page.getByRole('button', { name: 'Save settings' }).click()
  await expect(guest.getByText('Total rounds: 2')).toBeVisible()
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
    page.getByRole('heading', { name: 'Match started', exact: true }),
  ).toBeVisible({ timeout: 10000 })
  await expect(
    guest.getByRole('heading', { name: 'Match started', exact: true }),
  ).toBeVisible()
  const response = await page.request.get(
    `${backendUrl}/api/v1/lobbies/${lobbyId}`,
  )
  const lobby = (await response.json()) as {
    gameId: string
    hostPlayerId: string
    players: { id: string }[]
  }
  return { context, guest, lobby, lobbyId }
}
async function state(page: Page, gameId: string) {
  const response = await page.request.get(
    `${backendUrl}/api/v1/games/${gameId}/state`,
  )
  expect(response.status()).toBe(200)
  return (await response.json()) as GameState
}
test('refresh and a network interruption restore the same identity, score, round, turn and missed bid', async ({
  page,
  browser,
}) => {
  const { context, guest, lobby, lobbyId } = await startParty(page, browser)
  try {
    await page.getByRole('button', { name: /European Capitals.*easy/i }).click()
    await page.getByLabel('Your bid').fill('1')
    await page.getByRole('button', { name: 'Place bid', exact: true }).click()
    await guest.getByRole('button', { name: 'Call bluff', exact: true }).click()
    await page.getByLabel('Your word').fill('Stockholm')
    await page.getByRole('button', { name: 'Submit word', exact: true }).click()
    await expect(
      guest.getByRole('button', { name: /European Capitals.*easy/i }),
    ).toBeVisible()
    const before = await state(guest, lobby.gameId)
    const score = before.players.find(
      (player) => player.id === lobby.hostPlayerId,
    )!.score
    expect(score).toBeGreaterThan(0)
    await page.reload()
    await expect(page.getByLabel('Match scores')).toContainText(
      `Host: ${score} points`,
    )
    await expect(
      page.getByText('Connected to match.', { exact: true }),
    ).toBeVisible()
    expect((await state(page, lobby.gameId)).round?.id).toBe(before.round?.id)
    await page.context().setOffline(true)
    await expect(page.getByRole('status')).toContainText(
      'Reconnecting to match',
    )
    await expect(guest.getByLabel('Match scores')).toContainText(
      'Reconnecting',
      { timeout: 10000 },
    )
    await guest
      .getByRole('button', { name: /European Capitals.*easy/i })
      .click()
    await guest.getByLabel('Your bid').fill('1')
    await guest.getByRole('button', { name: 'Place bid', exact: true }).click()
    await page.context().setOffline(false)
    await expect(
      page.getByRole('button', { name: 'Call bluff', exact: true }),
    ).toBeEnabled({ timeout: 15000 })
    const after = await state(page, lobby.gameId)
    expect(after.round?.id).toBe(before.round?.id)
    expect(after.round?.activePlayerId).toBe(lobby.hostPlayerId)
    expect(after.round?.highestBid?.wordCount).toBe(1)
    expect(
      after.players.find((player) => player.id === lobby.hostPlayerId)?.score,
    ).toBe(score)
    const room = await page.request.get(
      `${backendUrl}/api/v1/lobbies/${lobbyId}`,
    )
    expect(
      ((await room.json()) as { players: { id: string }[] }).players.map(
        (player) => player.id,
      ),
    ).toEqual(lobby.players.map((player) => player.id))
    await page.getByRole('button', { name: 'Call bluff', exact: true }).click()
    await guest.getByLabel('Your word').fill('Stockholm')
    await guest
      .getByRole('button', { name: 'Submit word', exact: true })
      .click()
    await expect(
      page.getByRole('heading', { name: 'Match results', exact: true }),
    ).toBeVisible()
  } finally {
    await page.context().setOffline(false)
    await context.close()
  }
})
test('an expired guest cookie cannot reclaim a private match or leave controls active', async ({
  page,
  browser,
}) => {
  const { context } = await startParty(page, browser)
  try {
    const cookie = (await page.context().cookies()).find(
      (value) => value.name === 'wordgame.player',
    )
    expect(cookie).toBeDefined()
    await page.context().addCookies([{ ...cookie!, expires: 1 }])
    await page.reload()
    await expect(page.getByRole('alert')).toContainText(
      'This lobby is no longer available.',
    )
    await expect(page.getByRole('button', { name: 'Leave match' })).toHaveCount(
      0,
    )
    await expect(page.getByRole('button', { name: 'Place bid' })).toHaveCount(0)
    await expect(page.getByText('Loading lobby…', { exact: true })).toHaveCount(
      0,
    )
  } finally {
    await context.close()
  }
})
