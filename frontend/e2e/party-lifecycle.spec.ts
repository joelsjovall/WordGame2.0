import { expect, test, type Browser } from '@playwright/test'
import { backendUrl } from './support/environment.ts'
import { checkAccessibility } from './support/accessibility.ts'

for (const playerCount of [2, 3, 4]) {
  test(`${playerCount} players complete a private match, rematch with fresh scores, and can decline by leaving`, async ({
    page,
    browser,
  }) => {
    test.setTimeout(60000)
    const peerBrowsers: Browser[] = []
    async function peerContext() {
      // Independent devices do not share WebKit's per-process HTTP/1.1 connection pool.
      const peer =
        browser.browserType().name() === 'webkit'
          ? await browser.browserType().launch()
          : browser
      if (peer !== browser) peerBrowsers.push(peer)
      return peer.newContext({
        viewport: page.viewportSize() ?? { width: 390, height: 844 },
      })
    }
    await page.goto('/lobbies/new')
    await page.getByLabel('Your display name').fill('Host')
    await page
      .getByRole('button', { name: 'Create lobby', exact: true })
      .click()
    const invitation = await page.getByLabel('Invitation link').inputValue()
    const lobbyId = page.url().split('/').at(-1)!
    const context = await peerContext()
    try {
      const guest = await context.newPage()
      await guest.goto(invitation)
      await guest.getByLabel('Your display name').fill('Bob')
      await guest
        .getByRole('button', { name: 'Join lobby', exact: true })
        .click()
      const extraContexts = []
      const guests = [guest]
      try {
        for (const name of ['Carol', 'Dan'].slice(0, playerCount - 2)) {
          const extra = await peerContext()
          extraContexts.push(extra)
          const participant = await extra.newPage()
          guests.push(participant)
          await participant.goto(invitation)
          await participant.getByLabel('Your display name').fill(name)
          await participant
            .getByRole('button', { name: 'Join lobby', exact: true })
            .click()
        }
        await expect(
          page.getByText(`${playerCount} of 4 players`),
        ).toBeVisible()
        await page.getByLabel('Total rounds').fill('1')
        await page.getByLabel('Challenge time (seconds)').fill('5')
        await page.getByLabel('medium', { exact: true }).uncheck()
        await page.getByLabel('hard', { exact: true }).uncheck()
        await page.getByRole('button', { name: 'Save settings' }).click()
        await expect(guest.getByText('Total rounds: 1')).toBeVisible()
        const games: string[] = []
        for (let match = 0; match < 2; match++) {
          await expect(
            page.getByRole('button', { name: 'Ready', exact: true }),
          ).toBeEnabled()
          await expect(
            guest.getByRole('button', { name: 'Ready', exact: true }),
          ).toBeEnabled()
          await page.getByRole('button', { name: 'Ready', exact: true }).click()
          await expect(
            guest
              .getByLabel('Lobby players')
              .getByRole('listitem')
              .filter({ hasText: 'Host (host)' }),
          ).toContainText('Connected; Ready')
          for (const participant of guests) {
            await expect(
              participant.getByRole('button', { name: 'Ready', exact: true }),
            ).toBeEnabled()
            await participant
              .getByRole('button', { name: 'Ready', exact: true })
              .click()
            const identity =
              guests.indexOf(participant) === 0
                ? 'Bob'
                : ['Carol', 'Dan'][guests.indexOf(participant) - 1]!
            await expect(
              page
                .getByLabel('Lobby players')
                .getByRole('listitem')
                .filter({ hasText: identity }),
            ).toContainText('Connected; Ready')
          }
          await page.getByRole('button', { name: 'Start match' }).click()
          await expect(page.getByText(/Match starts in/)).toBeVisible()
          await expect(guest.getByText(/Match starts in/)).toBeVisible()
          await expect(
            page.getByRole('heading', { name: 'Match started', exact: true }),
          ).toBeVisible({ timeout: 10000 })
          await expect(
            guest.getByRole('heading', { name: 'Match started', exact: true }),
          ).toBeVisible()
          for (const participant of guests) {
            await expect(
              participant.getByRole('heading', {
                name: 'Match started',
                exact: true,
              }),
            ).toBeVisible()
            await expect(
              participant.getByLabel('Match scores').getByRole('listitem'),
            ).toHaveCount(playerCount)
          }
          await expect(page).toHaveURL(new RegExp(`/lobbies/${lobbyId}/game$`))
          await expect(page.getByLabel('Match scores')).toContainText(
            'Host: 0 points',
          )
          await expect(page.getByLabel('Match scores')).toContainText(
            'Bob: 0 points',
          )
          const room = await page.request.get(
            `${backendUrl}/api/v1/lobbies/${lobbyId}`,
          )
          games.push(((await room.json()) as { gameId: string }).gameId)
          await page
            .getByRole('button', { name: /European Capitals.*easy/i })
            .click()
          await page.getByLabel('Your bid').fill('1')
          await page
            .getByRole('button', { name: 'Place bid', exact: true })
            .click()
          await guest
            .getByRole('button', { name: 'Call bluff', exact: true })
            .click()
          await page.getByLabel('Your word').fill('Stockholm')
          await page
            .getByRole('button', { name: 'Submit word', exact: true })
            .click()
          await expect(
            page.getByRole('heading', { name: 'Match results', exact: true }),
          ).toBeVisible()
          await expect(
            guest.getByRole('heading', { name: 'Match results', exact: true }),
          ).toBeVisible()
          for (const participant of guests) {
            await expect(
              participant.getByRole('heading', {
                name: 'Match results',
                exact: true,
              }),
            ).toBeVisible()
            await expect(
              participant.getByLabel('Final standings').getByRole('listitem'),
            ).toHaveCount(playerCount)
            await expect(
              participant.getByRole('heading', {
                name: 'Winner: Host',
                exact: true,
              }),
            ).toBeVisible()
          }
          await expect(page.getByLabel('Final standings')).toContainText(
            'Host:',
          )
          if (playerCount === 2 && match === 0) await checkAccessibility(page)
          const winner = page.locator(
            '.game-standings [data-winner="true"] .game-rank',
          )
          await page.emulateMedia({ reducedMotion: 'no-preference' })
          await expect(winner).toHaveCSS('animation-name', 'game-victory')
          await expect(winner).toHaveCSS('animation-duration', '0.44s')
          await page.emulateMedia({ reducedMotion: 'reduce' })
          await expect(winner).toHaveCSS('animation-name', 'none')
          await expect(
            page.getByRole('heading', { name: 'Winner: Host', exact: true }),
          ).toBeVisible()
          if (match === 0) {
            await page
              .getByRole('button', { name: 'Play Again', exact: true })
              .click()
            await expect(
              page.getByRole('heading', { name: 'Private lobby', exact: true }),
            ).toBeVisible()
            await expect(
              guest.getByRole('heading', {
                name: 'Private lobby',
                exact: true,
              }),
            ).toBeVisible()
            await expect(page.getByLabel('Total rounds')).toHaveValue('1')
            await expect(
              page.getByRole('button', { name: 'Start match' }),
            ).toBeDisabled()
          }
        }
        expect(games[0]).not.toBe(games[1])
        await guest
          .getByRole('button', { name: 'Leave lobby', exact: true })
          .click()
        await expect(
          guest.getByRole('heading', { name: 'WordGame 2.0', exact: true }),
        ).toBeVisible()
        await page
          .getByRole('button', { name: 'Play Again', exact: true })
          .click()
        await expect(
          page.getByText(`${playerCount - 1} of 4 players`),
        ).toBeVisible()
        await expect(
          page.getByRole('button', { name: 'Start match' }),
        ).toBeDisabled()
        expect(
          await page.evaluate(
            () => document.documentElement.scrollWidth <= window.innerWidth,
          ),
        ).toBe(true)
      } finally {
        await Promise.all(extraContexts.map((extra) => extra.close()))
      }
    } finally {
      await context.close()
      await Promise.all(peerBrowsers.map((peer) => peer.close()))
    }
  })
}
