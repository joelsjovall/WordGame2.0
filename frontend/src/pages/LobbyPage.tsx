import { useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { ApiError } from '../services/api/client.ts'
import {
  lobbiesApi,
  type Lobby,
  type LobbySettings,
} from '../services/api/lobbies.ts'
import { useLobby } from '../features/lobby/useLobby.ts'
import { InvitationPanel } from '../features/lobby/InvitationPanel.tsx'
import { LobbySettingsForm } from '../features/lobby/LobbySettingsForm.tsx'
import { LobbyMembers } from '../features/lobby/LobbyMembers.tsx'
import '../features/lobby/lobby.css'

export function LobbyPage() {
  const { lobbyId = '' } = useParams()
  return <LobbyRoom key={lobbyId} id={lobbyId} />
}
function LobbyRoom({ id }: { id: string }) {
  const navigate = useNavigate()
  const {
    lobby,
    playerId,
    connection,
    error: loadingError,
    accept,
    unavailable,
  } = useLobby(id)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef(false)
  async function act(operation: () => Promise<Lobby>, after?: () => void) {
    if (pending.current) return
    pending.current = true
    setBusy(true)
    setError('')
    try {
      accept(await operation())
      after?.()
    } catch (failure) {
      setError(
        failure instanceof ApiError
          ? failure.message
          : 'Unable to reach the server. Please try again.',
      )
      // Recover a stale version before allowing another explicit write.
      try {
        accept(await lobbiesApi.get(id))
      } catch {
        /* Keep the original action error. */
      }
    } finally {
      pending.current = false
      setBusy(false)
    }
  }
  if (!lobby || unavailable)
    return (
      <main className="page">
        <h1>Private lobby</h1>
        <p role={loadingError ? 'alert' : 'status'}>
          {loadingError || 'Loading lobby…'}
        </p>
        <Link to="/join">Join a lobby</Link>
      </main>
    )
  const host = playerId === lobby.hostPlayerId
  const waiting = lobby.status === 'waiting'
  const settings = lobby.settings
  const self = lobby.players.find((player) => player.id === playerId)
  const save = (value: LobbySettings) =>
    act(() => lobbiesApi.configure(id, lobby.version, value))
  return (
    <main className="page">
      <h1>Private lobby</h1>
      <p>
        Lobby code: <strong>{lobby.lobbyCode}</strong>
      </p>
      <p role="status">{connection}</p>
      <p>
        {lobby.players.length} of {lobby.maximumPlayers} players
      </p>
      <LobbyMembers
        lobby={lobby}
        playerId={playerId}
        busy={busy || Boolean(loadingError)}
        remove={(target) => void act(() => lobbiesApi.remove(id, target))}
      />
      {waiting && self && (
        <div className="lobby-actions">
          <button
            type="button"
            disabled={busy || Boolean(loadingError) || !self.isConnected}
            onClick={() =>
              void act(() =>
                lobbiesApi.setReady(id, lobby.version, !self.isReady),
              )
            }
          >
            {self.isReady ? 'Not Ready' : 'Ready'}
          </button>
          <button
            type="button"
            disabled={busy}
            onClick={() =>
              void act(
                () => lobbiesApi.leave(id),
                () => {
                  void navigate('/')
                },
              )
            }
          >
            Leave lobby
          </button>
        </div>
      )}
      {(error || loadingError) && <p role="alert">{error || loadingError}</p>}
      {host && waiting ? (
        <LobbySettingsForm
          key={JSON.stringify(lobby.settings)}
          lobby={lobby}
          busy={busy || Boolean(loadingError)}
          save={save}
        />
      ) : (
        <section className="lobby-card" aria-label="Match settings">
          <h2>Match settings</h2>
          <p>Maximum players: {settings.maximumPlayers}</p>
          <p>
            Total rounds: {settings.roundCount ?? 'Automatic (two per player)'}
          </p>
          <p>Challenge time: {settings.challengeSeconds} seconds</p>
          <p>Difficulties: {settings.allowedDifficulties.join(', ')}</p>
          {!host && waiting && (
            <p>Only the host can change settings or start the match.</p>
          )}
        </section>
      )}
      {host && waiting && (
        <>
          <button
            className="lobby-start"
            disabled={busy || !lobby.canStart || Boolean(loadingError)}
            onClick={() => void act(() => lobbiesApi.start(id, lobby.version))}
          >
            Start match
          </button>
          {lobby.players.length < lobby.minimumPlayers && (
            <p>At least two players must join before starting.</p>
          )}
          {lobby.players.length >= lobby.minimumPlayers && !lobby.canStart && (
            <p>Every player must be connected and ready before starting.</p>
          )}
          <p>Save any edited settings before starting.</p>
        </>
      )}
      {lobby.status === 'starting' && <p role="status">Starting match…</p>}
      {lobby.gameId && (
        <section>
          <h2>Match started</h2>
          <p>
            The server has started the match with these settings. Gameplay
            controls will arrive in a later issue.
          </p>
        </section>
      )}
      {waiting && <InvitationPanel lobby={lobby} />}
      <p>
        <Link to="/">Back to homepage</Link>
      </p>
    </main>
  )
}
