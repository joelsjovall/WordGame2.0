import { apiClient } from './client.ts'
import type { components } from './generated.ts'

type Category = components['schemas']['CategoryResponse']
type CategoryList = components['schemas']['CategoryListResponse']
type Difficulty = components['schemas']['ApiCategoryDifficulty']

export function createCategoriesApi(client = apiClient) {
  return {
    list(difficulty?: Difficulty, signal?: AbortSignal): Promise<CategoryList> {
      const query = difficulty
        ? `?difficulty=${encodeURIComponent(difficulty)}`
        : ''
      return client.request(
        `/api/v1/categories${query}`,
        signal ? { signal } : {},
      )
    },
    get(categoryId: string, signal?: AbortSignal): Promise<Category> {
      return client.request(
        `/api/v1/categories/${encodeURIComponent(categoryId)}`,
        signal ? { signal } : {},
      )
    },
  }
}

export const categoriesApi = createCategoriesApi()
