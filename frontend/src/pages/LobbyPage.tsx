import { useEffect } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { lobbiesApi } from '../services/api/lobbies.ts'
import { useLobby } from '../features/lobby/useLobby.ts'
import { useLobbyActions } from '../features/lobby/useLobbyActions.ts'
import { InvitationPanel } from '../features/lobby/InvitationPanel.tsx'
import { LobbyMembers } from '../features/lobby/LobbyMembers.tsx'
import { LobbyMatchSettings } from '../features/lobby/LobbyMatchSettings.tsx'
import { LobbyScreenState } from '../features/lobby/LobbyScreenState.tsx'
import { ServerCountdown } from '../features/game/ServerCountdown.tsx'
import '../features/lobby/lobby.css'
import '../features/lobby/party-lobby.css'

export function LobbyPage() {
  const { lobbyId = '' } = useParams()
  return <LobbyRoom key={lobbyId} id={lobbyId} />
}
function LobbyRoom({ id }: { id: string }) {
  const navigate = useNavigate()
  const {
    lobby,
    activity,
    playerId,
    connection,
    error: loadingError,
    accept,
    unavailable,
  } = useLobby(id)
  const { act, error, busy } = useLobbyActions(id, accept)
  useEffect(() => {
    if (
      !unavailable &&
      (lobby?.status === 'started' || lobby?.status === 'completed')
    )
      void navigate(`/lobbies/${id}/game`, { replace: true })
  }, [lobby?.status, unavailable, navigate, id])
  if (!lobby || unavailable) return <LobbyScreenState error={loadingError} />
  const host = playerId === lobby.hostPlayerId
  const waiting = lobby.status === 'waiting'
  const self = lobby.players.find((player) => player.id === playerId)
  const blocked = busy || Boolean(loadingError)
  return (
    <main className="page party-lobby">
      <header className="party-heading">
        <Link to="/" className="party-back">
          ← WordGame
        </Link>
        <div className="party-title-row">
          <div>
            <p className="party-eyebrow">Party Mode · Private room</p>
            <h1>Private lobby</h1>
            <p className="party-subtitle">
              Good company. A little bluffing. Let’s play.
            </p>
          </div>
          <p
            className={`party-connection ${connection === 'Live' ? 'is-live' : ''}`}
            role="status"
          >
            <span aria-hidden="true" />
            {connection}
          </p>
        </div>
      </header>
      {(error || loadingError) && (
        <p className="party-error" role="alert">
          {error || loadingError}
        </p>
      )}
      <div className="party-grid">
        <div className="party-main">
          {waiting && <InvitationPanel lobby={lobby} />}
          {lobby.status === 'starting' && (
            <section className="party-surface party-countdown">
              <p className="party-eyebrow">Get ready</p>
              <h2>Everyone’s in.</h2>
              <p role="status">
                Match starts in{' '}
                {lobby.startsAt ? (
                  <ServerCountdown
                    key={lobby.startsAt}
                    deadline={lobby.startsAt}
                    serverTime={lobby.serverTime}
                  />
                ) : (
                  '…'
                )}{' '}
                seconds
              </p>
            </section>
          )}
          <section
            className="party-surface"
            aria-labelledby="party-players-title"
          >
            <div className="party-section-heading">
              <h2 id="party-players-title">Players</h2>
              <p className="party-count">
                {lobby.players.length} of {lobby.maximumPlayers} players
              </p>
            </div>
            <LobbyMembers
              lobby={lobby}
              playerId={playerId}
              busy={blocked}
              remove={(target) => void act(() => lobbiesApi.remove(id, target))}
            />
            <p
              className="party-activity"
              role="log"
              aria-live="polite"
              aria-atomic="true"
            >
              {activity}
            </p>
            {waiting && self && (
              <div className="party-ready-area">
                <p className="party-note">
                  {self.isReady
                    ? 'You’re ready. Waiting for the rest of the room.'
                    : 'All set? Let the room know you’re ready.'}
                </p>
                <div className="lobby-actions">
                  <button
                    className={`party-ready ${self.isReady ? 'is-ready' : ''}`}
                    type="button"
                    aria-pressed={self.isReady}
                    disabled={blocked || !self.isConnected}
                    onClick={() =>
                      void act(() =>
                        lobbiesApi.setReady(id, lobby.version, !self.isReady),
                      )
                    }
                  >
                    {self.isReady ? 'Not Ready' : 'Ready'}
                  </button>
                  <button
                    className="party-leave"
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
              </div>
            )}
          </section>
        </div>
        <aside className="party-settings">
          <LobbyMatchSettings
            lobby={lobby}
            host={host}
            busy={blocked}
            save={(settings) =>
              act(() => lobbiesApi.configure(id, lobby.version, settings))
            }
            start={() => void act(() => lobbiesApi.start(id, lobby.version))}
          />
        </aside>
      </div>
    </main>
  )
}
