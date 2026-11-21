import { useEffect, useSyncExternalStore } from 'react'
import { gameAudio } from '../services/gameAudio.ts'
import { Button } from './ui.tsx'
import './audio-settings.css'

export function AudioSettings() {
  const settings = useSyncExternalStore(
    gameAudio.subscribe,
    gameAudio.getSnapshot,
  )
  const enabled = !settings.muted && settings.ready
  useEffect(() => {
    const hide = () => {
      if (document.visibilityState !== 'visible') gameAudio.stop()
    }
    document.addEventListener('visibilitychange', hide)
    return () => document.removeEventListener('visibilitychange', hide)
  }, [])
  return (
    <aside className="audio-settings" aria-label="Game audio settings">
      <Button
        variant="secondary"
        aria-pressed={enabled}
        onClick={() => {
          if (enabled) gameAudio.mute()
          else void gameAudio.enable()
        }}
      >
        {enabled ? 'Mute sounds' : 'Enable sounds'}
      </Button>
      <label htmlFor="sound-volume">
        Sound volume: {Math.round(settings.volume * 100)}%
      </label>
      <input
        id="sound-volume"
        type="range"
        min="0"
        max="100"
        step="5"
        value={Math.round(settings.volume * 100)}
        onChange={(event) =>
          gameAudio.setVolume(Number(event.target.value) / 100)
        }
      />
      <p>
        {settings.blocked
          ? 'Audio is unavailable or blocked. You can still play without sound.'
          : 'Sounds are optional. Enable them here to allow audio in this tab.'}
      </p>
    </aside>
  )
}
