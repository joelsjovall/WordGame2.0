import { expect, test } from '@playwright/test'

test('a raised bid expires after invalid and duplicate answers, awarding the caller', async ({
  page,
  browser,
}) => {
  test.setTimeout(60_000)
  const rivalContext = await browser.newContext()
  try {
    await page.goto('/lobbies/new')
    await page.getByLabel('Your display name').fill('Host')
    await page
      .getByRole('button', { name: 'Create lobby', exact: true })
      .click()
    const invitation = await page.getByLabel('Invitation link').inputValue()

    const rival = await rivalContext.newPage()
    await rival.goto(invitation)
    await rival.getByLabel('Your display name').fill('Rival')
    await rival.getByRole('button', { name: 'Join lobby', exact: true }).click()

    await page.getByLabel('Total rounds').fill('1')
    await page.getByLabel('Challenge time (seconds)').fill('12')
    await page.getByLabel('medium', { exact: true }).uncheck()
    await page.getByLabel('hard', { exact: true }).uncheck()
    await page.getByRole('button', { name: 'Save settings' }).click()
    await page.getByRole('button', { name: 'Ready', exact: true }).click()
    await rival.getByRole('button', { name: 'Ready', exact: true }).click()
    await page.getByRole('button', { name: 'Start match' }).click()
    await expect(
      page.getByRole('heading', { name: 'Match started' }),
    ).toBeVisible()
    await page.getByRole('button', { name: /European Capitals.*easy/i }).click()
    await page.getByLabel('Your bid').fill('1')
    await page.getByRole('button', { name: 'Place bid', exact: true }).click()
    await rival.getByLabel('Your bid').fill('2')
    await rival.getByRole('button', { name: 'Place bid', exact: true }).click()
    await expect(page.getByText('Highest bid: 2 words')).toBeVisible()
    await page.getByRole('button', { name: 'Call bluff', exact: true }).click()

    async function submit(word: string) {
      await rival.getByLabel('Your word').fill(word)
      await rival
        .getByRole('button', { name: 'Submit word', exact: true })
        .click()
    }
    await submit('Stockholm')
    await submit('STOCKHOLM')
    await submit('Not a capital')
    await expect(rival.getByLabel('Submitted words')).toContainText('duplicate')
    await expect(rival.getByLabel('Submitted words')).toContainText('rejected')
    await expect(
      rival.getByRole('progressbar', { name: 'Accepted word progress' }),
    ).toHaveAttribute('value', '1')
    await expect(
      page.getByRole('heading', { name: 'Match results' }),
    ).toBeVisible({ timeout: 20_000 })
    await expect(
      page.getByRole('heading', { name: 'Winner: Host' }),
    ).toBeVisible()
    await expect(page.getByLabel('Final standings')).toContainText(
      'Host: 50 points',
    )
    await expect(page.getByLabel('Final standings')).toContainText(
      'Rival: 1 points',
    )
  } finally {
    await Promise.all(rivalContext.pages().map((rival) => rival.close()))
    await rivalContext.close()
  }
})
