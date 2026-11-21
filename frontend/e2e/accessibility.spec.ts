import { expect, test } from '@playwright/test'
import { checkAccessibility } from './support/accessibility.ts'

test('entry pages, rule dialog, waiting lobby and matchmaking pass accessibility checks', async ({
  page,
}) => {
  for (const path of ['/', '/join', '/lobbies/new', '/online']) {
    await page.goto(path)
    await expect(page.locator('main')).toBeVisible()
    if (path === '/online')
      await expect(page.getByLabel('Your display name')).toBeVisible()
    await checkAccessibility(page)
  }
  await page.goto('/')
  await page.keyboard.press('Tab')
  const skip = page.getByRole('link', { name: 'Skip to main content' })
  await expect(skip).toBeFocused()
  await expect(skip).toBeInViewport()
  await expect(skip).toHaveCSS('outline-style', 'solid')
  await skip.press('Enter')
  await expect(page.locator('#main-content')).toBeFocused()
  await page.getByRole('button', { name: 'How to Play', exact: true }).click()
  await checkAccessibility(page)
  await page.keyboard.press('Escape')
  await page.goto('/lobbies/new')
  await page.getByLabel('Your display name').fill('Accessibility host')
  await page.getByRole('button', { name: 'Create lobby', exact: true }).click()
  await expect(page.getByLabel('Invitation link')).toBeVisible()
  await page.evaluate(() => {
    document.documentElement.style.fontSize = '200%'
  })
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth + 1,
    ),
  ).toBe(true)
  await checkAccessibility(page)
  await page.getByRole('button', { name: 'Leave lobby', exact: true }).click()
  await page.goto('/online')
  await page.getByLabel('Your display name').fill('Accessibility player')
  await page.getByRole('button', { name: 'Play Online', exact: true }).click()
  await expect(
    page.getByRole('button', { name: 'Cancel Search' }),
  ).toBeVisible()
  await checkAccessibility(page)
  await page.getByRole('button', { name: 'Cancel Search' }).click()
})
