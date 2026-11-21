export type SoundCue =
  | 'category'
  | 'bid'
  | 'bluff'
  | 'accepted'
  | 'invalid'
  | 'warning'
  | 'round'
  | 'victory'
type Preferences = {
  muted: boolean
  volume: number
  ready: boolean
  blocked: boolean
}
const storageKey = 'wordgame.audio.v1'
// Original synthesized cues; no recordings, samples, or external assets.
const tones: Record<SoundCue, number[]> = {
  category: [392, 523],
  bid: [440, 660],
  bluff: [330, 220],
  accepted: [660, 880],
  invalid: [220, 165],
  warning: [440, 440],
  round: [523, 659, 784],
  victory: [523, 659, 784, 1046],
}

export function createGameAudio(dependencies: {
  context: () => AudioContext | null
  storage: () => Pick<Storage, 'getItem' | 'setItem'>
  visible: () => boolean
}) {
  let settings: Preferences = {
    muted: true,
    volume: 0.35,
    ready: false,
    blocked: false,
  }
  try {
    const saved: unknown = JSON.parse(
      dependencies.storage().getItem(storageKey) ?? 'null',
    )
    if (
      saved &&
      typeof saved === 'object' &&
      'muted' in saved &&
      'volume' in saved &&
      typeof saved.muted === 'boolean' &&
      typeof saved.volume === 'number' &&
      Number.isFinite(saved.volume)
    ) {
      settings = {
        ...settings,
        muted: saved.muted,
        volume: Math.max(0, Math.min(1, saved.volume)),
      }
    }
  } catch {
    /* Storage is optional, including in private browsing. */
  }
  let context: AudioContext | null = null
  let activation = 0
  const listeners = new Set<() => void>()
  const seen = new Set<string>()
  let active: OscillatorNode[] = []
  function update(change: Partial<Preferences>) {
    settings = { ...settings, ...change }
    try {
      dependencies
        .storage()
        .setItem(
          storageKey,
          JSON.stringify({ muted: settings.muted, volume: settings.volume }),
        )
    } catch {
      /* Optional persistence. */
    }
    listeners.forEach((listener) => listener())
  }
  function stop() {
    active.forEach((node) => {
      try {
        node.stop()
      } catch {
        /* Already finished. */
      }
    })
    active = []
  }
  return {
    subscribe(listener: () => void) {
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
      }
    },
    getSnapshot: () => settings,
    // Call only directly from an explicit user gesture; never from a game event.
    async enable() {
      const request = ++activation
      try {
        context ??= dependencies.context()
        if (!context) throw new Error('Audio unavailable')
        context.onstatechange = () =>
          update({ ready: context?.state === 'running' })
        await context.resume()
        if (request !== activation) return
        update({
          muted: false,
          ready: context.state === 'running',
          blocked: context.state !== 'running',
        })
      } catch {
        if (request === activation) update({ ready: false, blocked: true })
      }
    },
    mute() {
      activation++
      stop()
      update({ muted: true })
    },
    setVolume(volume: number) {
      if (!Number.isFinite(volume)) return
      stop()
      update({ volume: Math.max(0, Math.min(1, volume)) })
    },
    stop,
    play(id: string, cue: SoundCue) {
      if (seen.has(id)) return
      seen.add(id)
      if (seen.size > 256) seen.delete(seen.values().next().value!)
      if (
        settings.muted ||
        !settings.ready ||
        !settings.volume ||
        !dependencies.visible() ||
        !context
      )
        return
      if (context.state !== 'running') {
        update({ ready: false })
        return
      }
      stop()
      try {
        const audio = context
        tones[cue].forEach((frequency, index) => {
          const oscillator = audio.createOscillator()
          const gain = audio.createGain()
          const start = audio.currentTime + index * 0.09
          oscillator.type =
            cue === 'bluff' || cue === 'invalid' ? 'triangle' : 'sine'
          oscillator.frequency.value = frequency
          gain.gain.setValueAtTime(0, start)
          gain.gain.linearRampToValueAtTime(
            settings.volume * 0.12,
            start + 0.008,
          )
          gain.gain.linearRampToValueAtTime(0, start + 0.085)
          oscillator.connect(gain)
          gain.connect(audio.destination)
          oscillator.onended = () => {
            oscillator.disconnect()
            gain.disconnect()
            active = active.filter((node) => node !== oscillator)
          }
          active.push(oscillator)
          oscillator.start(start)
          oscillator.stop(start + 0.09)
        })
      } catch {
        stop()
        update({ ready: false, blocked: true })
      }
    },
  }
}

export const gameAudio = createGameAudio({
  context: () =>
    typeof AudioContext === 'undefined' ? null : new AudioContext(),
  storage: () => window.localStorage,
  visible: () => document.visibilityState === 'visible',
})
