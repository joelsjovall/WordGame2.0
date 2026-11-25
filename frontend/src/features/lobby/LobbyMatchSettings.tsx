import { useState } from 'react'
import { LobbySettingsForm } from './LobbySettingsForm.tsx'
import type { Lobby, LobbySettings } from '../../services/api/lobbies.ts'

export function LobbyMatchSettings({
  lobby,
  host,
  busy,
  save,
  start,
}: {
  lobby: Lobby
  host: boolean
  busy: boolean
  save: (settings: LobbySettings) => Promise<void>
  start: () => void
}) {
  const signature = JSON.stringify(lobby.settings)
  const [draft, setDraft] = useState({ base: signature, dirty: false })
  const dirty = draft.base === signature && draft.dirty
  const waiting = lobby.status === 'waiting'
  const settings = lobby.settings
  if (!host || !waiting)
    return (
      <section className="party-surface" aria-label="Match settings">
        <p className="party-eyebrow">The game plan</p>
        <h2>Match settings</h2>
        <div className="party-settings-summary">
          <p>Maximum players: {settings.maximumPlayers}</p>
          <p>
            Total rounds: {settings.roundCount ?? 'Automatic (two per player)'}
          </p>
          <p>Challenge time: {settings.challengeSeconds} seconds</p>
          <p>Difficulties: {settings.allowedDifficulties.join(', ')}</p>
          <p>Hidden finale: {settings.sealedOpening ? 'On' : 'Off'}</p>
          <p>
            Category pack:{' '}
            {settings.allowedCategoryIds
              ? `${settings.allowedCategoryIds.length} selected categories`
              : 'All categories'}
          </p>
        </div>
        {!host && waiting && (
          <p className="party-note">
            Only the host can change settings or start the match.
          </p>
        )}
      </section>
    )
  return (
    <section className="party-surface party-host" aria-label="Host controls">
      <p className="party-eyebrow">Host controls</p>
      <LobbySettingsForm
        key={signature}
        lobby={lobby}
        busy={busy}
        save={save}
        onDirtyChange={(value) => setDraft({ base: signature, dirty: value })}
      />
      <div className="party-start-area">
        <button
          className="lobby-start"
          disabled={busy || !lobby.canStart || dirty}
          onClick={start}
        >
          Start match
        </button>
        <p className="party-note">
          {dirty
            ? 'Save your settings before starting.'
            : lobby.players.length < lobby.minimumPlayers
              ? 'At least two players must join before starting.'
              : !lobby.canStart
                ? 'Every player must be connected and ready before starting.'
                : 'Everyone is ready. Let’s play.'}
        </p>
      </div>
    </section>
  )
}
