import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError } from '../services/api/client.ts'
import { lobbiesApi } from '../services/api/lobbies.ts'
import '../features/lobby/lobby.css'

export function CreateLobbyPage() {
  const navigate = useNavigate()
  const [requestId] = useState(() => crypto.randomUUID())
  const [name, setName] = useState('')
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
      const lobby = await lobbiesApi.create(
        { requestId, displayName: name },
        controller.signal,
      )
      if (!controller.signal.aborted) void navigate(`/lobbies/${lobby.id}`)
    } catch (failure) {
      if (!controller.signal.aborted)
        setError(
          failure instanceof ApiError
            ? failure.message
            : 'Unable to create the lobby. Please try again.',
        )
    } finally {
      pending.current = null
      if (!controller.signal.aborted) setBusy(false)
    }
  }
  return (
    <main className="page">
      <h1>Create a private lobby</h1>
      <p>No account needed. Invite friends and configure your match.</p>
      <form className="lobby-card" onSubmit={(event) => void submit(event)}>
        <label htmlFor="host-name">Your display name</label>
        <input
          id="host-name"
          autoComplete="nickname"
          required
          maxLength={32}
          value={name}
          disabled={busy}
          onChange={(event) => setName(event.target.value)}
        />
        {error && <p role="alert">{error}</p>}
        <button disabled={busy}>{busy ? 'Creating…' : 'Create lobby'}</button>
      </form>
      <Link to="/">Back to homepage</Link>
    </main>
  )
}
