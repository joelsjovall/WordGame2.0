const [baseUrl, path = '/health', requestCount = '500', concurrency = '20'] = process.argv.slice(2)
if (!baseUrl) {
  throw new Error('Usage: node http-load.mjs <local-base-url> [path] [requests] [concurrency]')
}

const total = Number(requestCount)
const workers = Number(concurrency)
if (!Number.isInteger(total) || total < 1 || !Number.isInteger(workers) || workers < 1) {
  throw new Error('Requests and concurrency must be positive integers.')
}
const endpoint = new URL(path, baseUrl)
if (!['127.0.0.1', 'localhost'].includes(endpoint.hostname)) {
  throw new Error('Performance probes may only target a local server.')
}

let next = 0
let errors = 0
const samples = []
const started = performance.now()
await Promise.all(Array.from({ length: Math.min(total, workers) }, async () => {
  while (next < total) {
    next++
    const requestStarted = performance.now()
    try {
      const response = await fetch(endpoint, { signal: AbortSignal.timeout(10_000) })
      await response.arrayBuffer()
      if (!response.ok) errors++
    } catch {
      errors++
    }
    samples.push(performance.now() - requestStarted)
  }
}))
samples.sort((a, b) => a - b)
const percentile = (fraction) => samples[Math.min(samples.length - 1, Math.ceil(samples.length * fraction) - 1)].toFixed(1)
const seconds = (performance.now() - started) / 1_000
console.log(`${endpoint.pathname}: ${total} requests, ${workers} concurrent, ${errors} errors`)
console.log(`p50 ${percentile(0.5)} ms, p95 ${percentile(0.95)} ms, ${Math.round(total / seconds)} requests/s`)
if (errors) process.exitCode = 1
