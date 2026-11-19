import { expect, test, type Browser, type Page } from '@playwright/test'
import { backendUrl } from './support/environment.ts'

async function create(page: Page) {
  await page.goto('/lobbies/new')
  await page.getByLabel('Your display name').fill('Host')
  await page.getByRole('button', { name: 'Create lobby', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Private lobby', exact: true }),
  ).toBeVisible()
  return await page.getByLabel('Invitation link').inputValue()
}
async function join(
  browser: Browser,
  host: Page,
  invitation: string,
  name: string,
) {
  const context = await browser.newContext({
    viewport: host.viewportSize() ?? { width: 390, height: 844 },
  })
  const page = await context.newPage()
  await page.goto(invitation)
  await page.getByLabel('Your display name').fill(name)
  await page.getByRole('button', { name: 'Join lobby', exact: true }).click()
  await page.getByRole('link', { name: 'Open lobby settings' }).click()
  await expect(
    page.getByRole('button', { name: 'Ready', exact: true }),
  ).toBeEnabled()
  return { page, context }
}
function member(page: Page, name: string) {
  return page
    .getByLabel('Lobby players')
    .getByRole('listitem')
    .filter({ hasText: name })
}

test('readiness, removal, and host departure synchronize across three guests', async ({
  page,
  browser,
}) => {
  const invitation = await create(page)
  const bob = await join(browser, page, invitation, 'Bob')
  const carol = await join(browser, page, invitation, 'Carol')
  try {
    await expect(page.getByText('3 of 4 players')).toBeVisible()
    await expect(member(page, 'Carol')).toContainText('Connected')
    await page.getByRole('button', { name: 'Ready', exact: true }).click()
    await expect(member(bob.page, 'Host (host)')).toContainText(
      'Connected; Ready',
    )
    await bob.page.getByRole('button', { name: 'Ready', exact: true }).click()
    await expect(member(carol.page, 'Bob')).toContainText('Connected; Ready')
    await carol.page.getByRole('button', { name: 'Ready', exact: true }).click()
    await expect(
      page.getByRole('button', { name: 'Start match' }),
    ).toBeEnabled()
    await bob.page
      .getByRole('button', { name: 'Not Ready', exact: true })
      .click()
    await expect(
      page.getByRole('button', { name: 'Start match' }),
    ).toBeDisabled()
    await page.getByRole('button', { name: 'Remove Bob' }).click()
    await expect(bob.page.getByRole('alert')).toHaveText(
      'You have left or been removed from this lobby.',
    )
    await expect(member(page, 'Bob')).toHaveCount(0)
    await expect(
      carol.page.getByRole('button', { name: 'Ready', exact: true }),
    ).toBeVisible()
    await bob.page.goto(invitation)
    await bob.page.getByLabel('Your display name').fill('Bob')
    await bob.page
      .getByRole('button', { name: 'Join lobby', exact: true })
      .click()
    await expect(bob.page.getByRole('alert')).toHaveText(
      'You have been removed from this lobby.',
    )
    await page.getByRole('button', { name: 'Leave lobby' }).click()
    await expect(
      page.getByRole('heading', { name: 'WordGame 2.0', exact: true }),
    ).toBeVisible()
    await expect(member(carol.page, 'Carol (host)')).toBeVisible()
    await expect(
      carol.page.getByRole('button', { name: 'Save settings' }),
    ).toBeVisible()
    await expect(
      carol.page.getByRole('button', { name: 'Start match' }),
    ).toBeDisabled()
    await carol.page.getByRole('button', { name: 'Leave lobby' }).click()
    await expect(
      carol.page.getByRole('heading', { name: 'WordGame 2.0', exact: true }),
    ).toBeVisible()
  } finally {
    await bob.context.close()
    await carol.context.close()
  }
})

test('multiple tabs preserve one slot and reconnecting guests retain their identity but must ready again', async ({
  page,
  browser,
}) => {
  const invitation = await create(page)
  const bob = await join(browser, page, invitation, 'Bob')
  try {
    const roomUrl = bob.page.url()
    const roomId = roomUrl.split('/').at(-1)!
    const initial = await page.request.get(
      `${backendUrl}/api/v1/lobbies/${roomId}`,
    )
    const before = (await initial.json()) as {
      players: { id: string; displayName: string }[]
    }
    const bobId = before.players.find(
      (player) => player.displayName === 'Bob',
    )!.id
    const anotherTab = await bob.context.newPage()
    await anotherTab.goto(roomUrl)
    await expect(
      anotherTab.getByRole('button', { name: 'Ready', exact: true }),
    ).toBeEnabled()
    await expect(page.getByText('2 of 4 players')).toBeVisible()
    await page.getByRole('button', { name: 'Ready', exact: true }).click()
    await expect(member(bob.page, 'Host (host)')).toContainText(
      'Connected; Ready',
    )
    await bob.page.getByRole('button', { name: 'Ready', exact: true }).click()
    await expect(
      page.getByRole('button', { name: 'Start match' }),
    ).toBeEnabled()
    await bob.page.close()
    await expect(
      anotherTab.getByRole('button', { name: 'Not Ready', exact: true }),
    ).toBeVisible()
    await anotherTab.close()
    await expect(member(page, 'Bob')).toContainText('Reconnecting; Not ready')
    await expect(
      page.getByRole('button', { name: 'Start match' }),
    ).toBeDisabled()
    const recovered = await bob.context.newPage()
    await recovered.goto(roomUrl)
    await expect(
      recovered.getByRole('button', { name: 'Ready', exact: true }),
    ).toBeEnabled()
    await expect(member(page, 'Bob')).toContainText('Connected; Not ready')
    const response = await recovered.request.get(
      `${backendUrl}/api/v1/lobbies/${roomId}`,
    )
    const after = (await response.json()) as {
      players: { id: string; displayName: string }[]
    }
    expect(after.players).toHaveLength(2)
    expect(
      after.players.find((player) => player.displayName === 'Bob')?.id,
    ).toBe(bobId)
    expect(
      await recovered.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true)
  } finally {
    await bob.context.close()
  }
})
