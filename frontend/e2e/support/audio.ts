import type { Page } from '@playwright/test'

declare global {
  interface Window {
    audioProbe: { contexts: number; voices: number; gesture: boolean }
  }
}
export async function observeAudio(page: Page) {
  await page.addInitScript(() => {
    window.audioProbe = { contexts: 0, voices: 0, gesture: false }
    const Original = window.AudioContext
    if (!Original) return
    window.AudioContext = class extends Original {
      constructor(options?: AudioContextOptions) {
        super(options)
        window.audioProbe.contexts++
      }
      override resume() {
        window.audioProbe.gesture = navigator.userActivation.isActive
        return super.resume()
      }
      override createOscillator() {
        window.audioProbe.voices++
        return super.createOscillator()
      }
    }
  })
}
