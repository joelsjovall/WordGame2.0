import type { components } from '../../services/api/generated.ts'

export type InvitationLobby = Pick<
  components['schemas']['LobbyResponse'],
  'id' | 'lobbyCode'
>

export function lobbyInvitationUrl(
  lobby: InvitationLobby,
  origin = window.location.origin,
) {
  return new URL(
    `/join/${encodeURIComponent(lobby.id)}/${encodeURIComponent(lobby.lobbyCode)}`,
    origin,
  ).href
}
