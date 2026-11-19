import { Link, useParams } from 'react-router'
import { JoinLobbyForm } from '../features/lobby/JoinLobbyForm.tsx'

export function JoinLobbyPage() {
  const { lobbyId, lobbyCode } = useParams()
  if (
    lobbyId &&
    lobbyCode &&
    (!/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(
      lobbyId,
    ) ||
      lobbyId === '00000000-0000-0000-0000-000000000000' ||
      !/^[A-HJ-NP-Z2-9]{6}$/i.test(lobbyCode))
  ) {
    return (
      <main className="page">
        <h1>Invalid invitation</h1>
        <p>
          Ask the host for a new invitation, or{' '}
          <Link to="/join">enter a lobby code</Link>.
        </p>
      </main>
    )
  }
  return (
    <main className="page">
      <JoinLobbyForm
        key={`${lobbyId ?? ''}/${lobbyCode ?? ''}`}
        invitation={lobbyId && lobbyCode ? { lobbyId, lobbyCode } : null}
      />
    </main>
  )
}
