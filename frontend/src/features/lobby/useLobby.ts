import type { Lobby } from '../../services/api/lobbies.ts'
import { connectLobby } from '../../services/lobbyConnection.ts'
import { lobbyActivity } from './lobbyActivity.ts'
import { useCallback, useEffect, useState } from 'react'

export function useLobby(id: string) {
  const [snapshot, setSnapshot] = useState<{
    lobby: Lobby | null
    activity: string
  }>({ lobby: null, activity: '' })
  const [playerId, setPlayerId] = useState('')
  const [error, setError] = useState('')
  const [unavailable, setUnavailable] = useState(false)
  const [connection, setConnection] = useState('Connecting…')
  const accept = useCallback((next: Lobby) => {
    if (!/^(0|[1-9]\d*)$/.test(next.version))
      throw new Error('Invalid lobby revision.')
    setSnapshot((previous) => {
      if (
        previous.lobby &&
        previous.lobby.id === next.id &&
        BigInt(next.version) < BigInt(previous.lobby.version)
      )
        return previous
      return {
        lobby: next,
        activity: lobbyActivity(previous.lobby, next) || previous.activity,
      }
    })
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
  return { ...snapshot, playerId, error, connection, unavailable, accept }
}
