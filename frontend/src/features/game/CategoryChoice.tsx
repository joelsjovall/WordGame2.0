import { useEffect, useState } from 'react'
import { categoriesApi } from '../../services/api/categories.ts'
import type { GameState } from '../../services/api/games.ts'
import type { components } from '../../services/api/generated.ts'

export function CategoryChoice({
  allowed,
  busy,
  choose,
}: {
  allowed: GameState['match']['allowedDifficulties']
  busy: boolean
  choose: (id: string) => void
}) {
  const [categories, setCategories] = useState<
    components['schemas']['CategoryResponse'][]
  >([])
  const [error, setError] = useState('')
  useEffect(() => {
    const controller = new AbortController()
    void categoriesApi
      .list(undefined, controller.signal)
      .then((result) => {
        if (!controller.signal.aborted) setCategories(result.items)
      })
      .catch(() => {
        if (!controller.signal.aborted)
          setError('Unable to load categories. Please reload.')
      })
    return () => controller.abort()
  }, [])
  return (
    <section className="lobby-card">
      <h2>Choose a category</h2>
      {error && <p role="alert">{error}</p>}
      {categories
        .filter((category) => !allowed || allowed.includes(category.difficulty))
        .map((category) => (
          <button
            key={category.id}
            disabled={busy}
            onClick={() => choose(category.id)}
          >
            {category.name} ({category.difficulty})
          </button>
        ))}
    </section>
  )
}
