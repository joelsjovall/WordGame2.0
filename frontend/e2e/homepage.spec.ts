import { expect, test } from '@playwright/test'

for (const width of [360, 768, 1280]) {
  test(`homepage explains modes and supports keyboard navigation at ${width}px`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 })
    const homeRequests: string[] = []
    page.on('request', (request) => {
      if (
        new URL(request.url()).pathname.startsWith('/api/') ||
        ['image', 'font'].includes(request.resourceType())
      )
        homeRequests.push(request.url())
    })
    await page.goto('/')
    await expect(
      page.getByRole('heading', { name: 'WordGame 2.0', exact: true }),
    ).toBeVisible()
    await expect(page.getByText('Party Mode', { exact: true })).toBeVisible()
    await expect(page.getByText('Online Mode', { exact: true })).toBeVisible()
    expect(homeRequests).toEqual([])
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    for (const name of ['Play with Friends', 'Play Online']) {
      const link = page.getByRole('link', { name, exact: true })
      expect((await link.boundingBox())!.height).toBeGreaterThanOrEqual(44)
    }
    await page.screenshot({
      path: testInfo.outputPath(`homepage-${width}.png`),
      fullPage: true,
    })
    const instructions = page.getByRole('button', { name: 'How to Play' })
    await instructions.focus()
    await page.keyboard.press('Enter')
    const rules = page.getByRole('dialog', { name: 'How to Play' })
    await expect(rules).toBeVisible()
    expect(homeRequests).toEqual([])
    await page.keyboard.press('Escape')
    await expect(rules).not.toBeVisible()
    const friends = page.getByRole('link', {
      name: 'Play with Friends',
      exact: true,
    })
    await friends.focus()
    await expect(friends).toHaveCSS('outline-style', 'solid')
    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(/\/lobbies\/new$/)
    await expect(
      page.getByRole('heading', { name: 'Create a private lobby' }),
    ).toBeVisible()
    await page.goto('/')
    await page.getByRole('link', { name: 'Play Online', exact: true }).click()
    await expect(page).toHaveURL(/\/online$/)
    await expect(page.getByLabel('Your display name')).toBeVisible()
  })
}

test('homepage remains usable with reduced motion and an unavailable backend', async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.route(/\/api\/v1\//, (route) => route.abort())
  await page.goto('/')
  await expect(page.locator('.home-hero')).toHaveCSS('animation-name', 'none')
  await page.getByRole('button', { name: 'How to Play' }).click()
  await expect(page.getByRole('dialog', { name: 'How to Play' })).toBeVisible()
  await page.getByRole('button', { name: 'Close instructions' }).click()
  await expect(
    page.getByRole('link', { name: 'Play with Friends', exact: true }),
  ).toBeEnabled()
  await expect(
    page.getByRole('link', { name: 'Play Online', exact: true }),
  ).toBeEnabled()
  await page.getByRole('link', { name: 'Join a private lobby' }).click()
  await expect(page).toHaveURL(/\/join$/)
})
