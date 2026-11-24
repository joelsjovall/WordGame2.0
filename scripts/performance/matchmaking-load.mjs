const [baseUrl, pairCountText = '10'] = process.argv.slice(2)
if (!baseUrl) throw new Error('Usage: node matchmaking-load.mjs <local-base-url> [pairs]')
const base = new URL(baseUrl)
if (!['127.0.0.1', 'localhost'].includes(base.hostname)) throw new Error('Local server only.')
const pairCount = Number(pairCountText)
if (!Number.isInteger(pairCount) || pairCount < 1 || pairCount > 100) throw new Error('Pairs must be 1–100.')

async function guest(index) {
  const response = await fetch(new URL('/api/v1/guests', base), {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: '{}',
  })
  if (!response.ok) throw new Error(`Guest ${index}: HTTP ${response.status}`)
  const cookie = response.headers.get('set-cookie')?.split(';', 1)[0]
  if (!cookie) throw new Error(`Guest ${index}: missing cookie`)
  return { index, cookie, searchId: null }
}

async function request(player, path, method = 'GET', body) {
  const response = await fetch(new URL(path, base), {
    method,
    headers: { cookie: player.cookie, ...(body ? { 'content-type': 'application/json' } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}),
    signal: AbortSignal.timeout(10_000),
  })
  if (!response.ok) throw new Error(`Player ${player.index} ${path}: HTTP ${response.status}`)
  return response.status === 204 ? null : response.json()
}

const started = performance.now()
const players = await Promise.all(Array.from({ length: pairCount * 2 }, (_, index) => guest(index)))
await Promise.all(players.map(async (player) => {
  const search = await request(player, '/api/v1/matchmaking/searches', 'POST', { displayName: `Load ${player.index}` })
  player.searchId = search.searchId
}))
const paired = await Promise.all(players.map(async (player) => {
  for (let attempt = 0; attempt < 50; attempt++) {
    const state = await request(player, '/api/v1/matchmaking/status')
    if (state.status === 'matched') return state
    await new Promise((resolve) => setTimeout(resolve, 100))
  }
  throw new Error(`Player ${player.index}: not matched`)
}))
await Promise.all(players.map((player) => request(player,
  `/api/v1/matchmaking/searches/${player.searchId}/accept`, 'POST', {})))
const games = await Promise.all(players.map(async (player) => {
  for (let attempt = 0; attempt < 50; attempt++) {
    const state = await request(player, '/api/v1/matchmaking/status')
    if (state.status === 'ready' && state.gameId) return state.gameId
    await new Promise((resolve) => setTimeout(resolve, 100))
  }
  throw new Error(`Player ${player.index}: match not ready`)
}))
if (new Set(games).size !== pairCount || new Set(paired.map((state) => state.matchId)).size !== pairCount) {
  throw new Error('Matchmaking produced the wrong number of distinct matches.')
}
console.log(`${pairCount} simultaneous Online matches ready in ${((performance.now() - started) / 1_000).toFixed(2)} s`)
