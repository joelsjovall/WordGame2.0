import { apiBaseUrl } from './config.ts'
import type { components } from './generated.ts'

type ProblemDetails = components['schemas']['ApiProblemDetails']
type JsonRequestOptions = Omit<RequestInit, 'body'> & { body?: unknown }

export class ApiError extends Error {
  readonly status: number
  readonly problem: Partial<ProblemDetails> | undefined

  constructor(status: number, problem?: Partial<ProblemDetails>) {
    super(problem?.title || `Request failed (${status}).`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export function apiErrorMessage(
  failure: unknown,
  fallback = 'Unable to reach the server. Please try again.',
) {
  return failure instanceof ApiError ? failure.message : fallback
}

function readProblem(value: unknown): Partial<ProblemDetails> | undefined {
  if (!value || typeof value !== 'object' || Array.isArray(value))
    return undefined
  const source = value as Record<string, unknown>
  const problem: Partial<ProblemDetails> = {}
  for (const key of [
    'type',
    'title',
    'detail',
    'instance',
    'traceId',
  ] as const) {
    if (typeof source[key] === 'string') problem[key] = source[key]
  }
  if (typeof source.status === 'number') problem.status = source.status
  if (
    source.errors &&
    typeof source.errors === 'object' &&
    !Array.isArray(source.errors)
  ) {
    const errors = Object.entries(source.errors)
    if (
      errors.every(
        ([, messages]) =>
          Array.isArray(messages) &&
          messages.every((item: unknown) => typeof item === 'string'),
      )
    ) {
      problem.errors = Object.fromEntries(errors) as Record<string, string[]>
    }
  }
  return Object.keys(problem).length ? problem : undefined
}

export function createApiClient(
  baseUrl = apiBaseUrl,
  fetchRequest: typeof fetch = globalThis.fetch,
) {
  const origin = baseUrl.trim().replace(/\/+$/, '')

  async function request<T>(
    path: string,
    options: JsonRequestOptions = {},
  ): Promise<T> {
    if (!path.startsWith('/') || path.startsWith('//') || path.includes('\\')) {
      throw new TypeError(
        'API paths must be relative paths beginning with one slash.',
      )
    }
    const { body, ...init } = options
    const headers = new Headers(init.headers)
    if (!headers.has('Accept')) headers.set('Accept', 'application/json')
    const encoded = body === undefined ? undefined : JSON.stringify(body)
    if (body !== undefined && encoded === undefined)
      throw new TypeError('The request body must be JSON serializable.')
    if (encoded !== undefined && !headers.has('Content-Type'))
      headers.set('Content-Type', 'application/json')

    const response = await fetchRequest(origin + path, {
      ...init,
      headers,
      ...(encoded === undefined ? {} : { body: encoded }),
    })
    if (!response.ok) {
      let problem: Partial<ProblemDetails> | undefined
      try {
        problem = readProblem(await response.json())
      } catch {
        // A proxy or server may return an empty/non-JSON error body.
      }
      throw new ApiError(response.status, problem)
    }
    if (response.status === 204 || init.method?.toUpperCase() === 'HEAD')
      return undefined as T
    return (await response.json()) as T
  }

  return { request }
}

export const apiClient = createApiClient()
