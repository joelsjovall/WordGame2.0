// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, expect, test, vi } from 'vitest'
import { spectatorsApi } from '../services/api/spectators.ts'
import { SpectatorPage } from './SpectatorPage.tsx'
import { gameFixture } from '../../tests/fixtures/game.ts'

afterEach(() => vi.restoreAllMocks())

test('spectators see match progress without gameplay controls or a player identity', async () => {
  const get = vi.spyOn(spectatorsApi, 'get').mockResolvedValue({
    lobbyStatus: 'started',
    game: gameFixture,
  })
  render(
    <MemoryRouter initialEntries={['/spectate/lobby/ABC234']}>
      <Routes>
        <Route
          path="/spectate/:lobbyId/:lobbyCode"
          element={<SpectatorPage />}
        />
      </Routes>
    </MemoryRouter>,
  )

  expect(
    await screen.findByRole('heading', { name: 'Watch Party match' }),
  ).toBeVisible()
  expect(await screen.findByText('Highest bid: 1 words')).toBeVisible()
  expect(screen.getByLabelText('Match scores')).toHaveTextContent('Host: 0 points')
  expect(screen.queryByRole('button', { name: 'Place bid' })).toBeNull()
  expect(get).toHaveBeenCalledWith('lobby', 'ABC234', expect.any(AbortSignal))
})
