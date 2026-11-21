import { expect, test, type Page } from '@playwright/test'
import { observeAudio } from './support/audio.ts'

async function noOverflow(page: Page) {
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
  ).toBe(true)
}

test('major entry pages reflow in portrait, landscape and at 200% text size', async ({
  page,
}) => {
  for (const viewport of [
    { width: 320, height: 640 },
    { width: 844, height: 390 },
  ]) {
    await page.setViewportSize(viewport)
    for (const path of [
      '/',
      '/lobbies/new',
      '/join',
      '/online',
      '/not-a-page',
    ]) {
      await page.goto(path)
      await expect(page.locator('main')).toBeVisible()
      await noOverflow(page)
      await page.evaluate(() => {
        document.documentElement.style.fontSize = '200%'
      })
      await noOverflow(page)
    }
  }
})

test('word entry keeps timer and submit reachable when the visible viewport shrinks', async ({
  page,
  browser,
  browserName,
}, testInfo) => {
  test.setTimeout(45000)
  await page.setViewportSize({ width: 390, height: 844 })
  if (browserName === 'chromium') {
    const session = await page.context().newCDPSession(page)
    await session.send('Emulation.setCPUThrottlingRate', { rate: 4 })
  }
  await page.emulateMedia({ reducedMotion: 'no-preference' })
  await observeAudio(page)
  await page.goto('/lobbies/new')
  const audioSupported = await page.evaluate(
    () => typeof AudioContext !== 'undefined',
  )
  await page.getByRole('button', { name: 'Enable sounds' }).click()
  if (audioSupported)
    await expect(
      page.getByRole('button', { name: 'Mute sounds' }),
    ).toBeVisible()
  else
    await expect(
      page.getByRole('complementary', { name: 'Game audio settings' }),
    ).toContainText('Audio is unavailable or blocked')
  await page
    .getByLabel('Your display name')
    .fill('Host with a long display name')
  await page.getByRole('button', { name: 'Create lobby', exact: true }).click()
  const invitation = await page.getByLabel('Invitation link').inputValue()
  const context = await browser.newContext({
    viewport: { width: 390, height: 844 },
  })
  try {
    const guest = await context.newPage()
    await guest.goto(invitation)
    await guest.getByLabel('Your display name').fill('Bob')
    await guest.getByRole('button', { name: 'Join lobby', exact: true }).click()
    await guest.getByRole('link', { name: 'Open lobby settings' }).click()
    await expect(
      page
        .getByLabel('Lobby players')
        .getByRole('listitem')
        .filter({ hasText: 'Bob' }),
    ).toContainText('Connected; Not ready')
    await page.getByRole('button', { name: 'Ready', exact: true }).click()
    await expect(
      guest
        .getByLabel('Lobby players')
        .getByRole('listitem')
        .filter({ hasText: 'Host with a long display name' }),
    ).toContainText('Connected; Ready')
    await guest.getByRole('button', { name: 'Ready', exact: true }).click()
    await page.getByRole('button', { name: 'Start match', exact: true }).click()
    await page.getByRole('button', { name: /European Capitals.*easy/i }).click()
    await expect(page.locator('.game-stage')).toHaveCSS(
      'animation-name',
      'game-arrive',
    )
    await page.getByLabel('Your bid').fill('2')
    await page.getByRole('button', { name: 'Place bid', exact: true }).click()
    await expect(guest.locator('.game-bid')).toContainText(
      'Highest bid: 2 words',
    )
    await expect(page.locator('.game-bid')).toHaveCSS(
      'animation-name',
      'game-emphasis',
    )
    if (audioSupported)
      await expect
        .poll(() => page.evaluate(() => window.audioProbe.voices))
        .toBeGreaterThanOrEqual(4)
    await guest.getByRole('button', { name: 'Call bluff', exact: true }).click()
    await expect(
      page.getByText('Bluff called · Time to prove it'),
    ).toBeVisible()
    await expect(page.locator('.game-bluff-called')).toHaveCSS(
      'animation-name',
      'game-emphasis',
    )
    await page.emulateMedia({ reducedMotion: 'reduce' })
    await expect(page.locator('.game-stage')).toHaveCSS(
      'animation-name',
      'none',
    )
    await expect(page.locator('.game-bluff-called')).toHaveCSS(
      'animation-name',
      'none',
    )
    await page.emulateMedia({ reducedMotion: 'no-preference' })
    await page.getByLabel('Your word').focus()
    // Browser automation does not open the native OS keyboard. Exercise its viewport contraction explicitly.
    await page.setViewportSize({ width: 390, height: 320 })
    await expect(page.locator('.game-challenge-entry')).toHaveAttribute(
      'data-compact',
      'true',
    )
    await expect(page.getByRole('timer')).toBeInViewport({ ratio: 1 })
    const input = page.getByLabel('Your word')
    await expect(input).toBeInViewport({ ratio: 1 })
    await input.fill('Stockholm')
    await expect(
      page.getByRole('button', { name: 'Submit word', exact: true }),
    ).toBeInViewport({ ratio: 1 })
    await noOverflow(page)
    await page.screenshot({
      path: testInfo.outputPath('compact-word-entry.png'),
    })
    await input.press('Enter')
    const words = page.getByLabel('Submitted words')
    await expect(words.getByText('accepted', { exact: true })).toBeVisible()
    await expect(words.locator('[data-status="accepted"]')).toHaveCSS(
      'animation-name',
      'game-emphasis',
    )
    await input.fill('Stockholm')
    await input.press('Enter')
    await expect(words.getByText('duplicate', { exact: true })).toBeVisible()
    await input.fill('Nope')
    await input.press('Enter')
    await expect(words.getByText('rejected', { exact: true })).toBeVisible()
    await expect(words.locator('[data-status="rejected"]')).toHaveCSS(
      'animation-name',
      'game-arrive',
    )
    await page.emulateMedia({ reducedMotion: 'reduce' })
    await expect(words.locator('[data-status="accepted"]')).toHaveCSS(
      'animation-name',
      'none',
    )
    await expect(words.locator('[data-status="duplicate"]')).toHaveCSS(
      'animation-name',
      'none',
    )
    await input.fill('Paris')
    await input.press('Enter')
    await expect(
      page.getByRole('heading', { name: 'Round complete', exact: true }),
    ).toBeVisible()
    await page.setViewportSize({ width: 844, height: 390 })
    await noOverflow(page)
    await page.setViewportSize({ width: 390, height: 844 })
    await expect(page.getByLabel('Match scores')).toContainText('points')
    await noOverflow(page)
  } finally {
    await context.close()
  }
})
