import { useEffect, useState, type FormEvent } from 'react'
import type { Lobby, LobbySettings } from '../../services/api/lobbies.ts'
import { categoriesApi } from '../../services/api/categories.ts'
import type { components } from '../../services/api/generated.ts'
import { categoryThemes, themes } from '../game/categoryThemes.ts'

export function LobbySettingsForm({
  lobby,
  save,
  busy,
  onDirtyChange,
}: {
  lobby: Lobby
  save: (settings: LobbySettings) => Promise<void>
  busy: boolean
  onDirtyChange?: (dirty: boolean) => void
}) {
  const [settings, setSettings] = useState(lobby.settings)
  const [categories, setCategories] = useState<
    components['schemas']['CategoryResponse'][]
  >([])
  const [categoryError, setCategoryError] = useState('')
  useEffect(() => {
    const controller = new AbortController()
    void categoriesApi.list(undefined, controller.signal).then(
      (result) => {
        if (!controller.signal.aborted) setCategories(result.items)
      },
      () => {
        if (!controller.signal.aborted)
          setCategoryError('Category packs are temporarily unavailable.')
      },
    )
    return () => controller.abort()
  }, [])
  const selectedIds = settings.allowedCategoryIds ?? null
  const pack =
    selectedIds === null
      ? 'All'
      : (themes.slice(1).find((theme) => {
          const ids = categories
            .filter((category) => categoryThemes[category.id] === theme)
            .map((category) => category.id)
          return (
            ids.length > 0 &&
            ids.length === selectedIds.length &&
            ids.every((id) => selectedIds.includes(id))
          )
        }) ?? 'Custom')
  function updateSettings(next: LobbySettings) {
    setSettings(next)
    onDirtyChange?.(
      next.maximumPlayers !== lobby.settings.maximumPlayers ||
        next.roundCount !== lobby.settings.roundCount ||
        next.challengeSeconds !== lobby.settings.challengeSeconds ||
        [...next.allowedDifficulties].sort().join('|') !==
          [...lobby.settings.allowedDifficulties].sort().join('|') ||
        [...(next.allowedCategoryIds ?? [])].sort().join('|') !==
          [...(lobby.settings.allowedCategoryIds ?? [])].sort().join('|'),
    )
  }
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
            updateSettings({
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
            updateSettings({
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
            updateSettings({
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
                  updateSettings({
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
        <label htmlFor="category-pack">Category pack</label>
        <select
          id="category-pack"
          value={pack}
          disabled={categories.length === 0}
          onChange={(event) => {
            const theme = event.target.value
            updateSettings({
              ...settings,
              allowedCategoryIds:
                theme === 'All'
                  ? null
                  : theme === 'Custom'
                    ? categories.map((category) => category.id)
                    : categories
                        .filter(
                          (category) => categoryThemes[category.id] === theme,
                        )
                        .map((category) => category.id),
            })
          }}
        >
          {themes.map((theme) => (
            <option key={theme} value={theme}>
              {theme === 'All' ? 'All categories' : theme}
            </option>
          ))}
          <option value="Custom">Choose categories</option>
        </select>
        {categoryError && <p role="alert">{categoryError}</p>}
        {pack === 'Custom' && (
          <fieldset>
            <legend>Categories in this private pack</legend>
            {categories.map((category) => (
              <label className="lobby-choice" key={category.id}>
                <input
                  type="checkbox"
                  checked={selectedIds?.includes(category.id) ?? false}
                  onChange={(event) =>
                    updateSettings({
                      ...settings,
                      allowedCategoryIds: event.target.checked
                        ? [...(selectedIds ?? []), category.id]
                        : (selectedIds ?? []).filter(
                            (id) => id !== category.id,
                          ),
                    })
                  }
                />
                {category.name} ({category.difficulty})
              </label>
            ))}
          </fieldset>
        )}
        <button
          type="submit"
          disabled={
            settings.allowedDifficulties.length === 0 ||
            settings.allowedCategoryIds?.length === 0
          }
        >
          Save settings
        </button>
      </fieldset>
    </form>
  )
}
