import { expect, test, type Page, type BrowserContext } from '@playwright/test'
import { backendUrl, frontendUrl } from './support/environment.ts'

async function join(page: Page, name: string) {
  await page.getByLabel('Your display name').fill(name)
  await expect(
    page.getByRole('button', { name: 'Play Online', exact: true }),
  ).toBeEnabled()
  await page.getByRole('button', { name: 'Play Online', exact: true }).click()
}
async function current(page: Page) {
  const response = await page.request.get(
    `${backendUrl}/api/v1/matchmaking/status`,
  )
  expect(response.ok()).toBe(true)
  return (await response.json()) as {
    searchId: string
    gameId: string | null
    status: string
  }
}
async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true)
  for (const button of await page.getByRole('button').all()) {
    if (await button.isVisible())
      expect((await button.boundingBox())!.height).toBeGreaterThanOrEqual(44)
  }
}

async function clearPendingSearch(context: BrowserContext) {
  try {
    await context.setOffline(false)
    const response = await context.request.get(
      `${backendUrl}/api/v1/matchmaking/status`,
    )
    if (!response.ok()) return
    const state = (await response.json()) as {
      status: string
      searchId: string | null
    }
    const path = `${backendUrl}/api/v1/matchmaking/searches/${state.searchId}`
    if (state.status === 'searching') await context.request.delete(path)
    if (state.status === 'matched')
      await context.request.post(`${path}/decline`, { data: {} })
  } catch {
    /* Do not mask an original browser assertion failure during cleanup. */
  }
}

test('Online players cancel, restore, decline, and enter the same game on desktop and mobile', async ({
  browser,
}, testInfo) => {
  test.setTimeout(120000)
  for (const width of [1280, 390]) {
    const options = {
      baseURL: frontendUrl,
      viewport: { width, height: 844 },
      isMobile: width < 768,
      hasTouch: width < 768,
    }
    const first = await browser.newContext(options)
    const second = await browser.newContext(options)
    try {
      const alice = await first.newPage()
      const bob = await second.newPage()
      await alice.goto('/')
      await alice
        .getByRole('link', { name: 'Play Online', exact: true })
        .click()
      await expect(
        alice.getByRole('heading', { name: 'Find your match', exact: true }),
      ).toBeVisible()
      await noOverflow(alice)
      await alice.screenshot({
        path: testInfo.outputPath(`online-idle-${width}.png`),
        fullPage: true,
      })
      await join(alice, 'Alice')
      await expect(
        alice.getByRole('heading', { name: 'Finding your next rival' }),
      ).toBeVisible()
      await expect(alice.getByText(/Search time:/)).toBeVisible()
      await alice.getByRole('button', { name: 'Cancel Search' }).click()
      await expect(alice.getByText(/Matchmaking cancelled/)).toBeVisible()
      await join(alice, 'Alice')
      const before = await current(alice)
      await alice.reload()
      await expect(
        alice.getByRole('heading', { name: 'Finding your next rival' }),
      ).toBeVisible()
      expect((await current(alice)).searchId).toBe(before.searchId)
      await first.setOffline(true)
      await expect(alice.getByRole('alert')).toContainText(
        /offline|interrupted/i,
      )
      await expect(
        alice.getByRole('button', { name: 'Cancel Search' }),
      ).toBeDisabled()
      await first.setOffline(false)
      await expect(
        alice.getByRole('button', { name: 'Cancel Search' }),
      ).toBeEnabled({ timeout: 15000 })
      expect((await current(alice)).searchId).toBe(before.searchId)
      await bob.goto('/online')
      await join(bob, 'Bob')
      await expect(
        alice.getByRole('heading', { name: 'Match found!' }),
      ).toBeVisible()
      await expect(
        bob.getByRole('heading', { name: 'Match found!' }),
      ).toBeVisible()
      await expect(alice.getByText('Bob', { exact: true })).toBeVisible()
      await expect(bob.getByText('Alice', { exact: true })).toBeVisible()
      await bob.getByRole('button', { name: 'Decline match' }).click()
      await expect(alice.getByText(/Matchmaking cancelled/)).toBeVisible()
      await expect(bob.getByText(/Matchmaking cancelled/)).toBeVisible()
      await join(alice, 'Alice')
      await join(bob, 'Bob')
      await expect(
        alice.getByRole('heading', { name: 'Match found!' }),
      ).toBeVisible()
      await expect(alice.getByText('Bob', { exact: true })).toBeVisible()
      await noOverflow(alice)
      await alice.screenshot({
        path: testInfo.outputPath(`online-found-${width}.png`),
        fullPage: true,
      })
      await alice.getByRole('button', { name: 'Accept match' }).click()
      await expect(
        alice.getByRole('button', { name: 'Accepted' }),
      ).toBeDisabled()
      await expect(alice).toHaveURL(/\/online$/)
      await bob.getByRole('button', { name: 'Accept match' }).click()
      await expect(alice).toHaveURL(/\/online\/games\/[a-f0-9-]+$/)
      await expect(bob).toHaveURL(alice.url())
      await expect(
        alice.getByRole('heading', { name: 'Match started', exact: true }),
      ).toBeVisible()
      await expect(
        bob.getByRole('heading', { name: 'Match started', exact: true }),
      ).toBeVisible()
      const room = await current(alice)
      expect(room.status).toBe('ready')
      expect(alice.url()).toContain(room.gameId)
      const gameResponse = await alice.request.get(
        `${backendUrl}/api/v1/games/${room.gameId}/state`,
      )
      const game = (await gameResponse.json()) as {
        match: { format: string; roundCount: number }
      }
      expect(game.match).toMatchObject({ format: 'online', roundCount: 6 })
      await alice.reload()
      await expect(alice.getByLabel('Match scores')).toContainText(
        'Alice: 0 points',
      )
      await alice
        .getByRole('button', { name: /European Capitals.*easy/i })
        .click()
      await alice.getByLabel('Your bid').fill('1')
      await alice
        .getByRole('button', { name: 'Place bid', exact: true })
        .click()
      await expect(
        bob.getByRole('button', { name: 'Call bluff', exact: true }),
      ).toBeEnabled()
      await bob.getByRole('button', { name: 'Call bluff', exact: true }).click()
      await alice.getByLabel('Your word').fill('Stockholm')
      await alice
        .getByRole('button', { name: 'Submit word', exact: true })
        .click()
      await expect(
        alice.getByRole('heading', { name: 'Round complete', exact: true }),
      ).toBeVisible()
      const scoredResponse = await alice.request.get(
        `${backendUrl}/api/v1/games/${room.gameId}/state`,
      )
      const scored = (await scoredResponse.json()) as {
        round: { id: string }
        players: { id: string; score: number }[]
      }
      expect(scored.players[0]!.score).toBeGreaterThan(0)
      await first.setOffline(true)
      await expect(bob.getByLabel('Match scores')).toContainText('Reconnecting')
      await first.setOffline(false)
      await expect(
        alice.getByText('Connected to match.', { exact: true }),
      ).toBeVisible({ timeout: 15000 })
      const restoredResponse = await alice.request.get(
        `${backendUrl}/api/v1/games/${room.gameId}/state`,
      )
      const restored = (await restoredResponse.json()) as typeof scored
      expect(restored.round.id).toBe(scored.round.id)
      expect(restored.players.map((player) => player.score)).toEqual(
        scored.players.map((player) => player.score),
      )
      await alice
        .getByRole('button', { name: 'Forfeit Match', exact: true })
        .click()
      await expect(
        bob.getByRole('heading', { name: 'You won', exact: true }),
      ).toBeVisible()
      await expect(
        alice.getByRole('heading', { name: 'Match over', exact: true }),
      ).toBeVisible()
      await expect(
        bob.getByText('Alice forfeited the match.', { exact: true }),
      ).toBeVisible()
      await alice.reload()
      await expect(
        alice.getByRole('heading', { name: 'Match over', exact: true }),
      ).toBeVisible()
      await expect(
        alice.getByRole('button', { name: 'Forfeit Match', exact: true }),
      ).toHaveCount(0)
      await noOverflow(alice)
      await alice
        .getByRole('button', { name: 'Request Rematch', exact: true })
        .click()
      await expect(
        bob.getByText('Your opponent requested a rematch.', { exact: false }),
      ).toBeVisible()
      await bob
        .getByRole('button', { name: 'Accept Rematch', exact: true })
        .click()
      await expect(alice).not.toHaveURL(new RegExp(`${room.gameId}$`))
      await expect(alice).toHaveURL(/\/online\/games\//)
      await expect(bob).toHaveURL(alice.url())
      await expect(
        alice.getByRole('button', { name: 'Forfeit Match', exact: true }),
      ).toBeEnabled()
      const freshId = alice.url().split('/').at(-1)!
      const freshResponse = await alice.request.get(
        `${backendUrl}/api/v1/games/${freshId}/state`,
      )
      const fresh = (await freshResponse.json()) as {
        players: { score: number }[]
        round: { number: number }
      }
      expect(fresh.players.every((player) => player.score === 0)).toBe(true)
      expect(fresh.round.number).toBe(1)
      for (let round = 1; round <= 6; round++) {
        const bidder = round % 2 === 1 ? alice : bob
        const caller = round % 2 === 1 ? bob : alice
        await bidder
          .getByRole('button', { name: /European Capitals.*easy/i })
          .click()
        await bidder.getByLabel('Your bid').fill('1')
        await bidder
          .getByRole('button', { name: 'Place bid', exact: true })
          .click()
        await caller
          .getByRole('button', { name: 'Call bluff', exact: true })
          .click()
        await bidder.getByLabel('Your word').fill('Stockholm')
        await bidder
          .getByRole('button', { name: 'Submit word', exact: true })
          .click()
        if (round < 6) {
          await expect(
            bidder.getByRole('heading', {
              name: 'Round complete',
              exact: true,
            }),
          ).toBeVisible()
        }
      }
      await expect(
        alice.getByRole('heading', { name: 'You won', exact: true }),
      ).toBeVisible()
      await expect(
        bob.getByRole('heading', { name: 'You won', exact: true }),
      ).toBeVisible()
      for (const player of [alice, bob]) {
        const statisticsResponse = await player.request.get(
          `${backendUrl}/api/v1/players/me/statistics`,
        )
        expect(statisticsResponse.ok()).toBe(true)
        const statistics = (await statisticsResponse.json()) as {
          matchesPlayed: number
          draws: number
          failedBluffChallenges: number
        }
        expect(statistics.matchesPlayed).toBe(2)
        expect(statistics.draws).toBe(1)
        expect(statistics.failedBluffChallenges).toBeGreaterThanOrEqual(3)
        const historyResponse = await player.request.get(
          `${backendUrl}/api/v1/players/me/history`,
        )
        const history = (await historyResponse.json()) as {
          items: { matchId: string; reason: string }[]
        }
        expect(history.items).toHaveLength(2)
        expect(history.items).toContainEqual(
          expect.objectContaining({ matchId: freshId, reason: 'completed' }),
        )
      }
      await bob
        .getByRole('button', { name: 'Request Rematch', exact: true })
        .click()
      await alice
        .getByRole('button', { name: 'Decline Rematch', exact: true })
        .click()
      await expect(
        bob.getByText('The rematch was declined.', { exact: true }),
      ).toBeVisible()
      await bob
        .getByRole('button', { name: 'Find New Opponent', exact: true })
        .click()
      await expect(bob).toHaveURL(/\/online$/)
      await expect(
        bob.getByRole('heading', {
          name: 'Finding your next rival',
          exact: true,
        }),
      ).toBeVisible()
      await bob
        .getByRole('button', { name: 'Cancel Search', exact: true })
        .click()
    } finally {
      await clearPendingSearch(first)
      await clearPendingSearch(second)
      await first.close()
      await second.close()
    }
  }
})
