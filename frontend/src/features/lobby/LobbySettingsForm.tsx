import { useState, type FormEvent } from 'react'
import type { Lobby, LobbySettings } from '../../services/api/lobbies.ts'

export function LobbySettingsForm({
  lobby,
  save,
  busy,
}: {
  lobby: Lobby
  save: (settings: LobbySettings) => Promise<void>
  busy: boolean
}) {
  const [settings, setSettings] = useState(lobby.settings)
  function submit(event: FormEvent) {
    event.preventDefault()
    void save(settings)
  }
  return (
    <form className="lobby-card" onSubmit={submit}>
      <h2>Match settings</h2>
      <fieldset disabled={busy}>
        <label htmlFor="maximum-players">Maximum players</label>
        <input
          id="maximum-players"
          type="number"
          min={Math.max(2, lobby.players.length)}
          max={4}
          required
          value={settings.maximumPlayers}
          onChange={(event) =>
            setSettings({
              ...settings,
              maximumPlayers: Number(event.target.value),
            })
          }
        />
        <label htmlFor="round-count">Total rounds</label>
        <input
          id="round-count"
          type="number"
          min={1}
          max={100}
          placeholder="Automatic"
          value={settings.roundCount ?? ''}
          onChange={(event) =>
            setSettings({
              ...settings,
              roundCount:
                event.target.value === '' ? null : Number(event.target.value),
            })
          }
        />
        <p>Leave blank for two rounds per player.</p>
        <label htmlFor="challenge-seconds">Challenge time (seconds)</label>
        <input
          id="challenge-seconds"
          type="number"
          min={5}
          max={300}
          required
          value={settings.challengeSeconds}
          onChange={(event) =>
            setSettings({
              ...settings,
              challengeSeconds: Number(event.target.value),
            })
          }
        />
        <fieldset>
          <legend>Available difficulties</legend>
          {(['easy', 'medium', 'hard'] as const).map((difficulty) => (
            <label className="lobby-choice" key={difficulty}>
              <input
                type="checkbox"
                checked={settings.allowedDifficulties.includes(difficulty)}
                onChange={(event) =>
                  setSettings({
                    ...settings,
                    allowedDifficulties: event.target.checked
                      ? [...settings.allowedDifficulties, difficulty]
                      : settings.allowedDifficulties.filter(
                          (value) => value !== difficulty,
                        ),
                  })
                }
              />
              {difficulty}
            </label>
          ))}
        </fieldset>
        <button
          type="submit"
          disabled={settings.allowedDifficulties.length === 0}
        >
          Save settings
        </button>
      </fieldset>
    </form>
  )
}
