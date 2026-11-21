import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { apiErrorMessage } from '../../services/api/client.ts'
import { lobbiesApi } from '../../services/api/lobbies.ts'
import './lobby.css'

export type LobbyInvitation = { lobbyId: string; lobbyCode: string }

export function JoinLobbyForm({
  invitation,
}: {
  invitation: LobbyInvitation | null
}) {
  const [code, setCode] = useState(invitation?.lobbyCode ?? '')
  const [name, setName] = useState('')
  const navigate = useNavigate()
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const pending = useRef<AbortController | null>(null)
  useEffect(() => () => pending.current?.abort(), [])

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (pending.current) return
    const controller = new AbortController()
    pending.current = controller
    setBusy(true)
    setError('')
    try {
      const joined = await lobbiesApi.join(
        {
          lobbyCode: code.trim(),
          displayName: name,
          ...(invitation ? { lobbyId: invitation.lobbyId } : {}),
        },
        controller.signal,
      )
      if (!controller.signal.aborted)
        void navigate(`/lobbies/${joined.id}`, { replace: true })
    } catch (failure) {
      if (!controller.signal.aborted) setError(apiErrorMessage(failure))
    } finally {
      if (pending.current === controller) pending.current = null
      if (!controller.signal.aborted) setBusy(false)
    }
  }

  return (
    <>
      <h1>Join a private lobby</h1>
      <p>Enter your name and a lobby code. No account needed.</p>
      <form
        className="lobby-card"
        onSubmit={(event) => void submit(event)}
        aria-busy={busy}
      >
        <label htmlFor="lobby-code">Lobby code</label>
        <input
          id="lobby-code"
          name="lobbyCode"
          value={code}
          onChange={(event) => setCode(event.target.value.trim().toUpperCase())}
          readOnly={invitation !== null}
          autoCapitalize="characters"
          autoComplete="off"
          spellCheck={false}
          required
          minLength={6}
          maxLength={32}
        />
        <label htmlFor="display-name">Your display name</label>
        <input
          id="display-name"
          name="displayName"
          value={name}
          onChange={(event) => setName(event.target.value)}
          autoComplete="nickname"
          required
          maxLength={32}
        />
        {error && <p role="alert">{error}</p>}
        <button type="submit" disabled={busy}>
          {busy ? 'Joining…' : 'Join lobby'}
        </button>
      </form>
      {invitation && (
        <p>
          <Link to="/join">Use a different code</Link>
        </p>
      )}
      <Link to="/">Back to homepage</Link>
    </>
  )
}
