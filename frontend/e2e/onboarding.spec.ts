import { expect, test } from '@playwright/test'

test('rules support keyboard, practice and mobile text reflow without navigation', async ({
  page,
}) => {
  await page.goto('/')
  const opener = page.getByRole('button', { name: 'How to Play', exact: true })
  await opener.focus()
  await page.keyboard.press('Enter')
  const rules = page.getByRole('dialog', { name: 'How to Play' })
  await expect(rules).toBeVisible()
  await expect(
    rules.getByRole('button', { name: 'Close instructions' }),
  ).toBeFocused()
  await page.keyboard.press('Tab')
  await expect(rules.getByRole('button', { name: 'Raise to 6' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(rules.getByRole('status')).toContainText(
    'now you claim you can name 6',
  )
  await page.keyboard.press('Tab')
  await expect(
    rules.getByRole('button', { name: "Call Bo's bluff" }),
  ).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(rules.getByRole('status')).toContainText('Correct!')
  for (const viewport of [
    { width: 320, height: 640 },
    { width: 844, height: 390 },
  ]) {
    await page.setViewportSize(viewport)
    await page.evaluate(() => {
      document.documentElement.style.fontSize = '200%'
    })
    expect(
      await rules.evaluate(
        (element) => element.scrollWidth <= element.clientWidth + 1,
      ),
    ).toBe(true)
    await rules
      .getByRole('button', { name: 'Back to play' })
      .scrollIntoViewIfNeeded()
    await expect(
      rules.getByRole('button', { name: 'Back to play' }),
    ).toBeInViewport()
  }
  await page.keyboard.press('Escape')
  await expect(rules).not.toBeVisible()
  await expect(opener).toBeFocused()
  await expect(page).toHaveURL('/')
  await opener.click()
  await rules.getByRole('button', { name: 'Close instructions' }).click()
  await expect(rules).not.toBeVisible()
})
