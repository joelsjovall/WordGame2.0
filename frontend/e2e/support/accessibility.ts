import AxeBuilder from '@axe-core/playwright'
import { expect, type Page } from '@playwright/test'

export async function checkAccessibility(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'])
    .analyze()
  // axe aggregates results in a temporary sibling tab. Restore the tested tab
  // before subsequent keyboard interactions, particularly in WebKit.
  await page.bringToFront()
  expect(results.violations).toEqual([])
}
