import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiError, createApiClient } from '../src/services/api/client.ts'
import { createCategoriesApi } from '../src/services/api/categories.ts'

test('JSON requests join the configured base, preserve headers and serialize only provided bodies', async () => {
  let url: string | undefined
  let options: RequestInit | undefined
  const client = createApiClient(
    ' https://api.example/backend/ ',
    async (input, init) => {
      url = String(input)
      options = init
      return Response.json({ version: '4' })
    },
  )
  const result = await client.request<{ version: string }>(
    '/api/v1/games/id/actions',
    {
      method: 'POST',
      headers: { 'X-Test': 'value' },
      body: { type: 'callBluff', actionId: 'id', expectedVersion: '3' },
    },
  )
  assert.equal(url, 'https://api.example/backend/api/v1/games/id/actions')
  assert.equal(new Headers(options?.headers).get('Accept'), 'application/json')
  assert.equal(
    new Headers(options?.headers).get('Content-Type'),
    'application/json',
  )
  assert.equal(new Headers(options?.headers).get('X-Test'), 'value')
  assert.deepEqual(JSON.parse(String(options?.body)), {
    type: 'callBluff',
    actionId: 'id',
    expectedVersion: '3',
  })
  assert.deepEqual(result, { version: '4' })
})

test('bodyless requests use same-origin URLs and do not add a Content-Type header', async () => {
  const client = createApiClient('', async (input, init) => {
    assert.equal(String(input), '/api/v1/categories')
    assert.equal(new Headers(init?.headers).has('Content-Type'), false)
    assert.equal(init?.body, undefined)
    return Response.json({ items: [] })
  })
  assert.deepEqual(await client.request('/api/v1/categories'), { items: [] })
})

test('204 responses do not attempt JSON parsing', async () => {
  const client = createApiClient(
    '',
    async () => new Response(null, { status: 204 }),
  )
  assert.equal(await client.request<void>('/api/v1/test'), undefined)
})

test('Problem Details exposes HTTP status, validation errors and trace ID through one error type', async () => {
  const client = createApiClient('', async () =>
    Response.json(
      {
        title: 'Invalid request.',
        status: 400,
        traceId: 'trace-123',
        errors: { difficulty: ['Use easy, medium, or hard exactly once.'] },
      },
      { status: 400 },
    ),
  )
  await assert.rejects(
    client.request('/api/v1/categories?difficulty=bad'),
    (error: unknown) => {
      assert.ok(error instanceof ApiError)
      assert.equal(error.status, 400)
      assert.equal(error.message, 'Invalid request.')
      assert.equal(error.problem?.traceId, 'trace-123')
      assert.deepEqual(error.problem?.errors?.difficulty, [
        'Use easy, medium, or hard exactly once.',
      ])
      return true
    },
  )
})

test('non-JSON server failures use a safe fallback message', async () => {
  const client = createApiClient(
    '',
    async () => new Response('<html>proxy failure</html>', { status: 502 }),
  )
  await assert.rejects(
    client.request('/api/v1/categories'),
    (error: unknown) => {
      assert.ok(error instanceof ApiError)
      assert.equal(error.status, 502)
      assert.equal(error.message, 'Request failed (502).')
      assert.equal(error.problem, undefined)
      return true
    },
  )
})

test('malformed error properties are ignored and HTTP status remains authoritative', async () => {
  const client = createApiClient('', async () =>
    Response.json(
      {
        status: 'wrong',
        title: 123,
        traceId: { invalid: true },
        errors: { word: [42] },
      },
      { status: 503 },
    ),
  )
  await assert.rejects(
    client.request('/api/v1/categories'),
    (error: unknown) => {
      assert.ok(error instanceof ApiError)
      assert.equal(error.status, 503)
      assert.equal(error.problem, undefined)
      return true
    },
  )
})

test('network errors and aborts propagate without retries', async () => {
  for (const expected of [
    new TypeError('offline'),
    new DOMException('cancelled', 'AbortError'),
  ]) {
    let attempts = 0
    const client = createApiClient('', async () => {
      attempts++
      throw expected
    })
    await assert.rejects(
      client.request('/api/v1/categories'),
      (error: unknown) => error === expected,
    )
    assert.equal(attempts, 1)
  }
})

test('category wrappers use generated contracts, encode IDs, and forward cancellation', async () => {
  const urls: string[] = []
  const cancellation = new AbortController()
  const client = createApiClient('https://api.example', async (input, init) => {
    urls.push(String(input))
    assert.equal(init?.signal, cancellation.signal)
    return Response.json({ id: 'id', name: 'Name', difficulty: 'hard' })
  })
  const categories = createCategoriesApi(client)
  const category = await categories.get('id/with spaces', cancellation.signal)
  assert.equal(category.difficulty, 'hard')
  await categories.list('hard', cancellation.signal)
  assert.deepEqual(urls, [
    'https://api.example/api/v1/categories/id%2Fwith%20spaces',
    'https://api.example/api/v1/categories?difficulty=hard',
  ])
})

test('absolute and protocol-relative paths cannot override the configured origin', async () => {
  const client = createApiClient('https://api.example', async () => {
    assert.fail('Unsafe paths must fail before fetch.')
  })
  for (const path of [
    'https://other.example/path',
    '//other.example/path',
    '/\\other.example/path',
  ]) {
    await assert.rejects(client.request(path), TypeError)
  }
})

test('invalid successful JSON is a protocol error rather than a fabricated API result', async () => {
  const client = createApiClient(
    '',
    async () => new Response('not JSON', { status: 200 }),
  )
  await assert.rejects(client.request('/api/v1/categories'), SyntaxError)
})
