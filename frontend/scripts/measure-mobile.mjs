import { chromium, devices } from '@playwright/test'

const target = new URL(process.argv[2] ?? 'http://127.0.0.1:5176/')
if (!['127.0.0.1', 'localhost'].includes(target.hostname)) {
  throw new Error('Mobile performance probes may only target a local server.')
}

const browser = await chromium.launch()
try {
  const samples = []
  for (let index = 0; index < 5; index++) {
    const context = await browser.newContext({ ...devices['Pixel 5'] })
    try {
      const page = await context.newPage()
      const cdp = await context.newCDPSession(page)
      await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 })
      const started = performance.now()
      await page.goto(target.href, { waitUntil: 'load' })
      await page
        .getByRole('heading', { name: 'WordGame 2.0', exact: true })
        .waitFor()
      samples.push(performance.now() - started)
    } finally {
      await context.close()
    }
  }
  samples.sort((a, b) => a - b)
  console.log(
    `Pixel 5 emulation, 4x CPU throttle, local network: median homepage ready ${samples[2].toFixed(0)} ms`,
  )
} finally {
  await browser.close()
}
