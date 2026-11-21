import { expect, test, vi } from 'vitest'
import { createGameAudio } from './gameAudio.ts'

function fixture() {
  const saved = new Map<string, string>()
  const storage = {
    getItem: (key: string) => saved.get(key) ?? null,
    setItem: (key: string, value: string) => {
      saved.set(key, value)
    },
  }
  const nodes: {
    frequency: { value: number }
    stop: ReturnType<typeof vi.fn>
    disconnect: ReturnType<typeof vi.fn>
  }[] = []
  const gain = {
    gain: { setValueAtTime: vi.fn(), linearRampToValueAtTime: vi.fn() },
    connect: vi.fn(),
    disconnect: vi.fn(),
  }
  const context = {
    state: 'running',
    currentTime: 1,
    destination: {},
    resume: vi.fn().mockResolvedValue(undefined),
    createOscillator: vi.fn(() => {
      const node = {
        frequency: { value: 0 },
        connect: vi.fn(),
        start: vi.fn(),
        stop: vi.fn(),
        disconnect: vi.fn(),
      }
      nodes.push(node)
      return node
    }),
    createGain: vi.fn(() => gain),
  }
  const dependencies = {
    context: vi.fn(() => context as unknown as AudioContext),
    storage: () => storage,
    visible: vi.fn(() => true),
  }
  return {
    audio: createGameAudio(dependencies),
    dependencies,
    context,
    nodes,
    gain,
    storage,
  }
}

test('audio is opt-in, remembers preferences, and still requires a gesture after reload', async () => {
  const { audio, dependencies, context, nodes, gain } = fixture()
  audio.play('early', 'bid')
  expect(dependencies.context).not.toHaveBeenCalled()
  audio.setVolume(0.5)
  await audio.enable()
  expect(context.resume).toHaveBeenCalledOnce()
  audio.play('bid1', 'bid')
  expect(nodes.map((node) => node.frequency.value)).toEqual([440, 660])
  expect(gain.gain.linearRampToValueAtTime).toHaveBeenCalledWith(0.06, 1.008)
  const reloaded = createGameAudio(dependencies)
  expect(reloaded.getSnapshot()).toMatchObject({
    muted: false,
    volume: 0.5,
    ready: false,
  })
  reloaded.play('after-reload', 'bid')
  expect(context.createOscillator).toHaveBeenCalledTimes(2)
  audio.mute()
  expect(nodes.every((node) => node.stop.mock.calls.length === 2)).toBe(true)
  expect(createGameAudio(dependencies).getSnapshot().muted).toBe(true)
})

test('duplicate events, muted, zero-volume and hidden events are dropped without a backlog', async () => {
  const { audio, context, dependencies } = fixture()
  await audio.enable()
  audio.play('one', 'accepted')
  audio.play('one', 'accepted')
  expect(context.createOscillator).toHaveBeenCalledTimes(2)
  audio.mute()
  audio.play('muted', 'victory')
  await audio.enable()
  audio.play('muted', 'victory')
  audio.setVolume(0)
  audio.play('zero', 'round')
  audio.setVolume(0.5)
  dependencies.visible.mockReturnValue(false)
  audio.play('hidden', 'bluff')
  dependencies.visible.mockReturnValue(true)
  audio.play('hidden', 'bluff')
  expect(context.createOscillator).toHaveBeenCalledTimes(2)
})

test('new cues stop prior voices and scheduled nodes disconnect when finished', async () => {
  const { audio, nodes, gain } = fixture()
  await audio.enable()
  audio.play('bid', 'bid')
  audio.play('bluff', 'bluff')
  expect(
    nodes.slice(0, 2).every((node) => node.stop.mock.calls.length === 2),
  ).toBe(true)
  expect(nodes.slice(2).map((node) => node.frequency.value)).toEqual([330, 220])
  const node = nodes[2] as unknown as OscillatorNode
  node.onended!(new Event('ended'))
  expect(node.disconnect).toHaveBeenCalled()
  expect(gain.disconnect).toHaveBeenCalled()
})

test('unsupported, blocked or suspended audio never throws into gameplay', async () => {
  const { audio, context, dependencies } = fixture()
  context.resume.mockRejectedValueOnce(new Error('NotAllowedError'))
  await expect(audio.enable()).resolves.toBeUndefined()
  expect(audio.getSnapshot()).toMatchObject({ ready: false, blocked: true })
  expect(() => audio.play('blocked', 'bid')).not.toThrow()
  await audio.enable()
  context.state = 'suspended'
  audio.play('suspended', 'round')
  expect(audio.getSnapshot().ready).toBe(false)
  const unsupported = createGameAudio({ ...dependencies, context: () => null })
  await expect(unsupported.enable()).resolves.toBeUndefined()
  expect(unsupported.getSnapshot().blocked).toBe(true)
})

test('invalid or unavailable storage falls back safely and volume is clamped', () => {
  const { storage, dependencies } = fixture()
  storage.setItem('wordgame.audio.v1', '{broken')
  const audio = createGameAudio(dependencies)
  expect(audio.getSnapshot()).toMatchObject({ muted: true, volume: 0.35 })
  audio.setVolume(2)
  expect(audio.getSnapshot().volume).toBe(1)
  audio.setVolume(Number.NaN)
  expect(audio.getSnapshot().volume).toBe(1)
  const unavailable = createGameAudio({
    ...dependencies,
    storage: () => {
      throw new Error('denied')
    },
  })
  expect(() => unavailable.setVolume(0.2)).not.toThrow()
})

test('muting during a pending activation keeps sounds muted', async () => {
  const { audio, context } = fixture()
  let finish!: () => void
  context.resume.mockReturnValueOnce(
    new Promise<void>((resolve) => {
      finish = resolve
    }),
  )
  const pending = audio.enable()
  audio.mute()
  finish()
  await pending
  expect(audio.getSnapshot().muted).toBe(true)
  audio.play('after-mute', 'victory')
  expect(context.createOscillator).not.toHaveBeenCalled()
})
