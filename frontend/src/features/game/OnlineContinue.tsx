import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import {
  rematchesApi,
  type RematchState,
  type RematchAction,
} from '../../services/api/rematches.ts'
import { matchmakingApi } from '../../services/api/matchmaking.ts'
import type { GameState } from '../../services/api/games.ts'

export function OnlineContinue({
  state,
  playerId,
}: {
  state: GameState
  playerId: string
}) {
  const [rematch, setRematch] = useState<RematchState | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [reload, setReload] = useState(0)
  const pending = useRef(false)
  const navigate = useNavigate()
  useEffect(() => {
    if (state.status !== 'completed') return
    const controller = new AbortController()
    void rematchesApi
      .get(state.id, controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) {
          setRematch(value)
          setError('')
        }
      })
      .catch(() => {
        if (!controller.signal.aborted)
          setError(
            'Unable to load rematch options. Retry or find a new opponent.',
          )
      })
    return () => controller.abort()
  }, [state.id, state.status, state.version, reload])
  useEffect(() => {
    if (rematch?.status === 'ready' && rematch.gameId)
      void navigate(`/online/games/${encodeURIComponent(rematch.gameId)}`, {
        replace: true,
      })
  }, [rematch, navigate])
  async function act(action: RematchAction | 'find') {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      if (action === 'find') {
        if (state.status === 'completed') {
          const result = await rematchesApi.respond(state.id, 'decline')
          if (result.status === 'ready' && result.gameId) {
            setRematch(result)
            return
          }
        }
        const name = state.players.find(
          (player) => player.id === playerId,
        )?.displayName
        if (!name) throw new Error('Player missing')
        await matchmakingApi.join(name)
        void navigate('/online')
      } else {
        setRematch(await rematchesApi.respond(state.id, action))
      }
    } catch {
      setError('Unable to continue. Please retry.')
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  const accepted = rematch?.acceptedPlayerIds.includes(playerId)
  return (
    <section className="online-card" aria-label="Continue playing">
      {error && (
        <p role="alert">
          {error}{' '}
          <button
            disabled={busy}
            onClick={() => setReload((value) => value + 1)}
          >
            Retry
          </button>
        </p>
      )}
      {rematch?.status === 'idle' && (
        <button disabled={busy} onClick={() => void act('request')}>
          Request Rematch
        </button>
      )}
      {rematch?.status === 'pending' && (
        <>
          <p role="status">
            {accepted
              ? 'Waiting for your opponent to agree…'
              : 'Your opponent requested a rematch.'}{' '}
            Requests expire after 30 seconds.
          </p>
          {!accepted && (
            <button disabled={busy} onClick={() => void act('accept')}>
              Accept Rematch
            </button>
          )}
          <button disabled={busy} onClick={() => void act('decline')}>
            Decline Rematch
          </button>
        </>
      )}
      {rematch?.status === 'declined' && (
        <p role="status">The rematch was declined.</p>
      )}
      {rematch?.status === 'expired' && (
        <p role="status">The rematch request expired.</p>
      )}
      {rematch?.status === 'unavailable' && (
        <p role="status">
          Your opponent is no longer available for this rematch.
        </p>
      )}
      {rematch?.status === 'ready' && (
        <p role="status">Starting your rematch…</p>
      )}
      <button
        disabled={busy || rematch?.status === 'ready'}
        onClick={() => void act('find')}
      >
        Find New Opponent
      </button>
    </section>
  )
}
