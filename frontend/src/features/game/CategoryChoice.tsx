import { useEffect, useState } from 'react'
import { categoriesApi } from '../../services/api/categories.ts'
import type { GameState } from '../../services/api/games.ts'
import type { components } from '../../services/api/generated.ts'
import { Button, Card, LoadingStatus } from '../../components/ui.tsx'
import { categoryThemes, themes, type Theme } from './categoryThemes.ts'

export function CategoryChoice({
  allowed,
  allowedCategoryIds,
  busy,
  choose,
}: {
  allowed: GameState['match']['allowedDifficulties']
  allowedCategoryIds: GameState['match']['allowedCategoryIds']
  busy: boolean
  choose: (id: string) => void
}) {
  const [categories, setCategories] = useState<
    components['schemas']['CategoryResponse'][]
  >([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [attempt, setAttempt] = useState(0)
  const [theme, setTheme] = useState<Theme>('All')
  useEffect(() => {
    const controller = new AbortController()
    void categoriesApi
      .list(undefined, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) {
          setCategories(result.items)
          setLoading(false)
        }
      })
      .catch(() => {
        if (!controller.signal.aborted) {
          setError('Unable to load categories. Please retry.')
          setLoading(false)
        }
      })
    return () => controller.abort()
  }, [attempt])
  return (
    <Card className="game-panel">
      <h2>Choose a category</h2>
      {loading && <LoadingStatus>Loading categories...</LoadingStatus>}
      {error && (
        <div role="alert">
          <p>{error}</p>
          <Button
            disabled={busy}
            onClick={() => {
              setError('')
              setLoading(true)
              setAttempt((value) => value + 1)
            }}
          >
            Retry categories
          </Button>
        </div>
      )}
      {categories.length > 0 && (
        <label className="game-category-theme">
          Theme
          <select
            value={theme}
            onChange={(event) => setTheme(event.target.value as Theme)}
          >
            {themes.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </label>
      )}
      {categories
        .filter(
          (category) =>
            (!allowed || allowed.includes(category.difficulty)) &&
            (!allowedCategoryIds || allowedCategoryIds.includes(category.id)) &&
            (theme === 'All' || categoryThemes[category.id] === theme),
        )
        .map((category) => (
          <Button
            variant="secondary"
            key={category.id}
            disabled={busy}
            onClick={() => choose(category.id)}
          >
            {category.name} ({category.difficulty})
          </Button>
        ))}
    </Card>
  )
}
