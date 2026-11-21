import { expect, test } from '@playwright/test'
import { observeAudio } from './support/audio.ts'

test('global audio is opt-in, user activated, adjustable, persistent and mutable', async ({
  page,
}) => {
  await observeAudio(page)
  await page.goto('/')
  const settings = page.getByRole('complementary', {
    name: 'Game audio settings',
  })
  await expect(
    settings.getByRole('button', { name: 'Enable sounds' }),
  ).toBeVisible()
  expect(await page.evaluate(() => window.audioProbe.contexts)).toBe(0)
  // Windows WebKit does not expose Web Audio; exercise its fallback rather than skip the journey.
  const supported = await page.evaluate(
    () => typeof AudioContext !== 'undefined',
  )
  await settings.getByRole('button', { name: 'Enable sounds' }).click()
  if (supported) {
    await expect(
      settings.getByRole('button', { name: 'Mute sounds' }),
    ).toHaveAttribute('aria-pressed', 'true')
    expect(await page.evaluate(() => window.audioProbe)).toMatchObject({
      contexts: 1,
      gesture: true,
      voices: 0,
    })
  } else {
    await expect(settings).toContainText('Audio is unavailable or blocked')
  }
  const volume = settings.getByRole('slider')
  await volume.fill('25')
  await expect(volume).toHaveValue('25')
  await page.reload()
  await expect(settings.getByRole('slider')).toHaveValue('25')
  await expect(
    settings.getByRole('button', { name: 'Enable sounds' }),
  ).toBeVisible()
  expect(await page.evaluate(() => window.audioProbe.contexts)).toBe(0)
  if (supported) {
    await settings.getByRole('button', { name: 'Enable sounds' }).click()
    await settings.getByRole('button', { name: 'Mute sounds' }).click()
  }
  await page.reload()
  await expect(
    settings.getByRole('button', { name: 'Enable sounds' }),
  ).toHaveAttribute('aria-pressed', 'false')
})

test('unavailable audio and storage do not block navigation or settings', async ({
  page,
}) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'AudioContext', { value: undefined })
    Object.defineProperty(window, 'localStorage', {
      get: () => {
        throw new Error('Storage denied')
      },
    })
  })
  await page.goto('/')
  await page.getByRole('button', { name: 'Enable sounds' }).click()
  await expect(
    page.getByText(
      'Audio is unavailable or blocked. You can still play without sound.',
    ),
  ).toBeVisible()
  await page
    .getByRole('link', { name: 'Play with Friends', exact: true })
    .click()
  await expect(page.getByLabel('Your display name')).toBeVisible()
})
