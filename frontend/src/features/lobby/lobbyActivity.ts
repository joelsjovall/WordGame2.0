import type { Lobby } from '../../services/api/lobbies.ts'

export function lobbyActivity(previous: Lobby | null, next: Lobby): string {
  if (!previous || previous.id !== next.id) return ''
  const joined = next.players
    .filter(
      (player) => !previous.players.some((member) => member.id === player.id),
    )
    .map((player) => player.displayName)
  const left = previous.players
    .filter((player) => !next.players.some((member) => member.id === player.id))
    .map((player) => player.displayName)
  const messages: string[] = []
  if (joined.length) messages.push(`${joined.join(', ')} joined the lobby.`)
  if (left.length) messages.push(`${left.join(', ')} left the lobby.`)
  if (previous.hostPlayerId !== next.hostPlayerId) {
    const host = next.players.find((player) => player.id === next.hostPlayerId)
    if (host) messages.push(`${host.displayName} is now the host.`)
  }
  return messages.join(' ')
}
