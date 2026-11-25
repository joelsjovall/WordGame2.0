// @vitest-environment jsdom
import { renderHook } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { gameFixture as game } from '../../../tests/fixtures/game.ts'
import type { GameState } from '../../services/api/games.ts'
import { gameAudio } from '../../services/gameAudio.ts'
import { useGameAudio } from './useGameAudio.ts'

afterEach(() => vi.restoreAllMocks())
test('only fresh authoritative events play, with silent initial/recovered snapshots', () => {
  const play = vi.spyOn(gameAudio, 'play').mockImplementation(() => {})
  const hook = renderHook(
    ({ state, connected }) => useGameAudio(state, 'host', connected),
    { initialProps: { state: game, connected: true } },
  )
  expect(play).not.toHaveBeenCalled()
  const bid: GameState = {
    ...game,
    round: {
      ...game.round!,
      highestBid: { playerId: 'bob', wordCount: 2, risk: false },
    },
  }
  hook.rerender({ state: bid, connected: true })
  expect(play).toHaveBeenLastCalledWith(
    expect.stringContaining('/bid/2'),
    'bid',
  )
  hook.rerender({ state: { ...bid, version: '7' }, connected: true })
  expect(play).toHaveBeenCalledTimes(1)
  const challenge: GameState = {
    ...bid,
    round: {
      ...bid.round!,
      status: 'challenge',
      challenge: {
        id: 'proof',
        playerId: 'host',
        callerId: 'bob',
        requiredWordCount: 3,
        acceptedWordCount: 0,
        startedAt: game.serverTime,
        deadline: game.serverTime,
        outcome: 'pending',
        submissions: [],
      },
    },
  }
  hook.rerender({ state: challenge, connected: true })
  expect(play).toHaveBeenLastCalledWith('proof/bluff', 'bluff')
  let current = challenge
  for (const status of ['accepted', 'rejected', 'duplicate'] as const) {
    current = {
      ...current,
      round: {
        ...current.round!,
        challenge: {
          ...current.round!.challenge!,
          submissions: [
            ...current.round!.challenge!.submissions,
            { text: 'word', status },
          ],
        },
      },
    }
    hook.rerender({ state: current, connected: true })
    expect(play).toHaveBeenLastCalledWith(
      expect.stringContaining('/word/'),
      status === 'accepted' ? 'accepted' : 'invalid',
    )
  }
  hook.rerender({ state: current, connected: false })
  play.mockClear()
  hook.rerender({
    state: { ...current, round: { ...current.round!, status: 'completed' } },
    connected: true,
  })
  expect(play).not.toHaveBeenCalled()
})

test('round completion and winner-only victory take priority over the final submitted word', () => {
  const play = vi.spyOn(gameAudio, 'play').mockImplementation(() => {})
  const hook = renderHook(
    ({ state, player }) => useGameAudio(state, player, true),
    { initialProps: { state: game, player: 'host' } },
  )
  const complete: GameState = {
    ...game,
    round: { ...game.round!, status: 'completed' },
  }
  hook.rerender({ state: complete, player: 'host' })
  expect(play).toHaveBeenLastCalledWith(
    expect.stringContaining('/complete'),
    'round',
  )
  const won: GameState = {
    ...complete,
    status: 'completed',
    result: {
      gameId: game.id,
      format: 'party',
      completedRoundCount: 1,
      isTie: false,
      winnerIds: ['host'],
      standings: [],
    },
  }
  hook.rerender({ state: won, player: 'host' })
  expect(play).toHaveBeenLastCalledWith(`${game.id}/victory`, 'victory')
  play.mockClear()
  hook.rerender({ state: game, player: 'bob' })
  hook.rerender({ state: won, player: 'bob' })
  expect(play).not.toHaveBeenCalledWith(expect.anything(), 'victory')
})
