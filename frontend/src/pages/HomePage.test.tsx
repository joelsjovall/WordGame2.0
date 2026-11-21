// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { expect, test } from 'vitest'
import { HomePage } from './HomePage.tsx'

test.each([
  ['Play with Friends', '/lobbies/new', 'Party setup'],
  ['Play Online', '/online', 'Online matchmaking'],
  ['Join a private lobby', '/join', 'Join by code'],
])(
  '%s navigates directly to its destination',
  async (label, path, destination) => {
    render(
      <MemoryRouter>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path={path} element={<h1>{destination}</h1>} />
        </Routes>
      </MemoryRouter>,
    )
    await userEvent.setup().click(screen.getByRole('link', { name: label }))
    expect(screen.getByRole('heading', { name: destination })).toBeVisible()
  },
)

test('mode descriptions and instructions explain the game without a request', () => {
  render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  )
  expect(screen.getByText('Party Mode')).toBeVisible()
  expect(screen.getByText('Online Mode')).toBeVisible()
  expect(screen.getByText('2–4 players')).toBeVisible()
  expect(screen.getByText('1 vs 1')).toBeVisible()
  expect(screen.getByRole('button', { name: 'How to Play' })).toBeVisible()
  expect(screen.getByRole('heading', { name: 'How to Play' })).toBeVisible()
  expect(screen.getByText('Beat the clock')).toBeVisible()
})
