import { expect, test } from '@playwright/test'
import { backendUrl } from './support/environment.ts'

test('homepage renders on a real browser with no runtime errors', async ({
  page,
}) => {
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(error.message))

  await page.goto('/')

  await expect(page).toHaveTitle('WordGame 2.0')
  await expect(
    page.getByRole('heading', { name: 'WordGame 2.0', exact: true }),
  ).toBeVisible()
  await expect(
    page.getByText('Party Mode and Online Mode are coming soon.'),
  ).toBeVisible()
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true)
  expect(errors).toEqual([])
})

test('a direct unknown URL can navigate home and survive refresh', async ({
  page,
}) => {
  await page.goto('/missing-page')
  await expect(
    page.getByRole('heading', { name: 'Page not found' }),
  ).toBeVisible()
  await page.getByRole('link', { name: 'Back to homepage' }).click()
  await expect(page).toHaveURL('/')
  await page.reload()
  await expect(
    page.getByRole('heading', { name: 'WordGame 2.0', exact: true }),
  ).toBeVisible()
})

test('local backend health and public category contracts work without production services', async ({
  request,
}) => {
  const health = await request.get(backendUrl + '/health')
  expect(health.status()).toBe(200)
  expect(await health.text()).toBe('Healthy')
  const categories = await request.get(backendUrl + '/api/v1/categories')
  expect(categories.status()).toBe(200)
  const body = await categories.json()
  expect(body.items.length).toBeGreaterThan(0)
  for (const category of body.items) {
    expect(category).not.toHaveProperty('answers')
    expect(category).not.toHaveProperty('aliases')
  }
})
