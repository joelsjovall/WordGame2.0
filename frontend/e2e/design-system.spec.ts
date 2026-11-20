import { expect, test } from '@playwright/test'

for (const width of [360, 768, 1280]) {
  test(`shared visual system stays readable and usable at ${width}px`, async ({
    page,
  }, testInfo) => {
    await page.setViewportSize({ width, height: 900 })
    await page.goto('/')
    await expect(
      page.getByRole('heading', { name: 'WordGame 2.0' }),
    ).toBeVisible()
    const play = page.getByRole('link', { name: 'Play Online' })
    expect((await play.boundingBox())!.height).toBeGreaterThanOrEqual(44)
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    await play.focus()
    await expect(play).toHaveCSS('outline-style', 'solid')
    await page.screenshot({
      path: testInfo.outputPath(`identity-${width}.png`),
      fullPage: true,
    })
    await page.keyboard.press('Enter')
    await expect(page.getByLabel('Your display name')).toBeVisible()
    await expect(
      page.getByRole('button', { name: 'Play Online' }),
    ).toBeDisabled()
    await page.getByLabel('Your display name').fill('Player')
    await expect(
      page.getByRole('button', { name: 'Play Online' }),
    ).toBeEnabled()
  })
}

test('semantic text and control colors meet contrast thresholds', async ({
  page,
}) => {
  await page.goto('/')
  const ratios = await page.evaluate(() => {
    const root = getComputedStyle(document.documentElement)
    const luminance = (token: string) => {
      const hex = root.getPropertyValue(token).trim().slice(1)
      const values = [0, 2, 4].map((offset) => {
        const value = parseInt(hex.slice(offset, offset + 2), 16) / 255
        return value <= 0.04045
          ? value / 12.92
          : ((value + 0.055) / 1.055) ** 2.4
      })
      return values[0]! * 0.2126 + values[1]! * 0.7152 + values[2]! * 0.0722
    }
    const contrast = (first: string, second: string) => {
      const a = luminance(first),
        b = luminance(second)
      return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)
    }
    return {
      text: [
        ['--color-ink', '--color-page'],
        ['--color-muted', '--color-surface'],
        ['--color-on-primary', '--color-primary'],
        ['--color-on-secondary', '--color-secondary'],
        ['--color-success', '--color-success-soft'],
        ['--color-warning', '--color-warning-soft'],
        ['--color-error', '--color-error-soft'],
        ['--color-on-primary', '--color-error'],
      ].map(([a, b]) => contrast(a!, b!)),
      controls: [
        contrast('--color-line', '--color-surface'),
        contrast('--color-focus', '--color-page'),
      ],
    }
  })
  for (const ratio of ratios.text) expect(ratio).toBeGreaterThanOrEqual(4.5)
  for (const ratio of ratios.controls) expect(ratio).toBeGreaterThanOrEqual(3)
})

test('native dialog supports keyboard dismissal and shared motion respects preferences', async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.goto('/')
  await page.evaluate(() => {
    // Exercise the reusable native dialog/loading styles without adding a production showcase route.
    const dialog = document.createElement('dialog')
    dialog.setAttribute('aria-label', 'Design system dialog')
    const button = document.createElement('button')
    button.textContent = 'Close dialog'
    button.addEventListener('click', () => dialog.close())
    const spinner = document.createElement('span')
    spinner.className = 'wg-spinner'
    spinner.setAttribute('aria-hidden', 'true')
    button.append(spinner)
    dialog.append(button)
    document.body.append(dialog)
    dialog.showModal()
  })
  const dialog = page.getByRole('dialog', { name: 'Design system dialog' })
  await expect(dialog).toBeVisible()
  await expect(page.getByRole('button', { name: 'Close dialog' })).toBeFocused()
  await expect(page.locator('.wg-spinner')).toHaveCSS('animation-name', 'none')
  expect((await dialog.boundingBox())!.width).toBeLessThanOrEqual(
    page.viewportSize()!.width,
  )
  await page.keyboard.press('Escape')
  await expect(dialog).not.toBeVisible()
})
