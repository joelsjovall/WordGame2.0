import { expect, test } from '@playwright/test'

for (const width of [360, 768, 1280]) {
  test(`lobby copy, readiness, feedback and host separation work at ${width}px`, async ({
    page,
    browser,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 })
    await page.addInitScript(() =>
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: {
          writeText: async (text: string) => {
            document.documentElement.dataset.copiedLobbyValue = text
          },
        },
      }),
    )
    await page.goto('/lobbies/new')
    await page
      .getByLabel('Your display name')
      .fill('AlexandriaWithAVeryLongPlayerName')
    await page
      .getByRole('button', { name: 'Create lobby', exact: true })
      .click()
    await expect(
      page.getByRole('heading', { name: 'Private lobby', exact: true }),
    ).toBeVisible()
    const code = await page.getByLabel('Lobby code').inputValue()
    await page.getByRole('button', { name: 'Copy code', exact: true }).click()
    await expect(page.locator('html')).toHaveAttribute(
      'data-copied-lobby-value',
      code,
    )
    const invitation = await page.getByLabel('Invitation link').inputValue()
    await page.getByRole('button', { name: 'Copy invitation link' }).click()
    await expect(page.locator('html')).toHaveAttribute(
      'data-copied-lobby-value',
      invitation,
    )
    await expect(
      page.getByRole('button', { name: 'Start match' }),
    ).toBeDisabled()
    await page.getByRole('button', { name: 'Ready', exact: true }).click()
    await expect(
      page.getByRole('button', { name: 'Not Ready', exact: true }),
    ).toHaveAttribute('aria-pressed', 'true')
    const targets = await page
      .locator(
        'button:visible, input[type="number"]:visible, .lobby-choice:visible',
      )
      .evaluateAll((elements) =>
        elements.map((element) => element.getBoundingClientRect().height),
      )
    expect(targets.every((height) => height >= 44)).toBe(true)
    const context = await browser.newContext({
      viewport: { width, height: 900 },
    })
    try {
      const guest = await context.newPage()
      await guest.goto(invitation)
      await guest.getByLabel('Your display name').fill('Taylor')
      await guest
        .getByRole('button', { name: 'Join lobby', exact: true })
        .click()
      await expect(
        guest.getByRole('region', { name: 'Match settings' }),
      ).toBeVisible()
      await expect(
        guest.getByRole('region', { name: 'Host controls' }),
      ).toHaveCount(0)
      await expect(page.getByRole('log')).toContainText(
        'Taylor joined the lobby.',
      )
      await expect(
        page.getByRole('button', { name: 'Ready', exact: true }),
      ).toBeVisible()
      expect(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
      ).toBe(true)
      expect(
        await guest.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
      ).toBe(true)
      await page.screenshot({
        path: testInfo.outputPath(`lobby-${width}.png`),
        fullPage: true,
      })
      await guest.getByRole('button', { name: 'Leave lobby' }).click()
      await expect(page.getByRole('log')).toContainText(
        'Taylor left the lobby.',
      )
    } finally {
      await context.close()
    }
  })
}
