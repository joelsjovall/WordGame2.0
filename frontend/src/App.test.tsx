// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from './App'

describe('application routes', () => {
  it('renders the homepage and explains the game without a backend', () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    )

    expect(
      screen.getByRole('heading', { level: 1, name: 'WordGame 2.0' }),
    ).toBeVisible()
    expect(
      screen.getByText('A multiplayer word-bidding and bluffing game.'),
    ).toBeVisible()
    expect(
      screen.getByText('Party Mode and Online Mode are coming soon.'),
    ).toBeVisible()
  })

  it('lets a player recover from an unknown route using the homepage link', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/missing-page']}>
        <App />
      </MemoryRouter>,
    )
    expect(
      screen.getByRole('heading', { name: 'Page not found' }),
    ).toBeVisible()

    await user.click(screen.getByRole('link', { name: 'Back to homepage' }))

    expect(
      await screen.findByRole('heading', { name: 'WordGame 2.0' }),
    ).toBeVisible()
    expect(
      screen.queryByRole('heading', { name: 'Page not found' }),
    ).not.toBeInTheDocument()
  })
})
