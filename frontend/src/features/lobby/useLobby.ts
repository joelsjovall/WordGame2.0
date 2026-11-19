import type { Lobby } from '../../services/api/lobbies.ts'
import { connectLobby } from '../../services/lobbyConnection.ts'
import { useCallback, useEffect, useState } from 'react'

export function useLobby(id: string) {
  const [lobby, setLobby] = useState<Lobby | null>(null)
  const [playerId, setPlayerId] = useState('')
  const [error, setError] = useState('')
  const [unavailable, setUnavailable] = useState(false)
  const [connection, setConnection] = useState('Connecting…')
  const accept = useCallback((next: Lobby) => {
    if (!/^(0|[1-9]\d*)$/.test(next.version))
      throw new Error('Invalid lobby revision.')
    setLobby((previous) =>
      !previous ||
      previous.id !== next.id ||
      BigInt(next.version) >= BigInt(previous.version)
        ? next
        : previous,
    )
  }, [])

  useEffect(
    () =>
      connectLobby(id, {
        state: accept,
        identity: setPlayerId,
        connection: setConnection,
        error: setError,
        unavailable: () => setUnavailable(true),
      }),
    [id, accept],
  )
  return { lobby, playerId, error, connection, unavailable, accept }
}
