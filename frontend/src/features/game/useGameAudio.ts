import { useEffect, useRef } from 'react'
import type { GameState } from '../../services/api/games.ts'
import { gameAudio } from '../../services/gameAudio.ts'

export function useGameAudio(
  state: GameState | null,
  playerId: string,
  connected: boolean,
) {
  const previous = useRef<GameState | null>(null)
  useEffect(() => {
    const before = previous.current
    previous.current = connected ? state : null
    // Initial/recovered snapshots establish a baseline rather than replaying missed events.
    if (!connected || !state || !before || before.id !== state.id) return
    const round = state.round
    if (
      state.result &&
      !before.result &&
      state.result.winnerIds.includes(playerId)
    ) {
      gameAudio.play(`${state.id}/victory`, 'victory')
    } else if (
      round?.status === 'completed' &&
      (before.round?.id !== round.id || before.round.status !== 'completed')
    ) {
      gameAudio.play(`${state.id}/${round.id}/complete`, 'round')
    } else if (
      round?.challenge &&
      round.challenge.id !== before.round?.challenge?.id
    ) {
      gameAudio.play(`${round.challenge.id}/bluff`, 'bluff')
    } else if (
      round?.challenge &&
      round.challenge.submissions.length >
        (before.round?.challenge?.submissions.length ?? 0)
    ) {
      const submission = round.challenge.submissions.at(-1)!
      gameAudio.play(
        `${round.challenge.id}/word/${round.challenge.submissions.length}`,
        submission.status === 'accepted' ? 'accepted' : 'invalid',
      )
    } else if (
      round?.highestBid &&
      (round.id !== before.round?.id ||
        round.highestBid.wordCount !== before.round?.highestBid?.wordCount)
    ) {
      gameAudio.play(
        `${state.id}/${round.id}/bid/${round.highestBid.wordCount}`,
        'bid',
      )
    } else if (
      round?.category &&
      (round.id !== before.round?.id || !before.round?.category)
    ) {
      gameAudio.play(`${state.id}/${round.id}/category`, 'category')
    }
  }, [state, playerId, connected])
}
