import { useEffect, useState } from 'react'
import { gameAudio } from '../../services/gameAudio.ts'

export function ServerCountdown({
  deadline,
  serverTime,
  showUrgency = false,
  soundId,
}: {
  deadline: string
  serverTime: string
  showUrgency?: boolean
  soundId?: string
}) {
  const [remaining, setRemaining] = useState(() =>
    Math.max(0, Date.parse(deadline) - Date.parse(serverTime)),
  )
  useEffect(() => {
    const initial = Math.max(0, Date.parse(deadline) - Date.parse(serverTime))
    const received = performance.now()
    const timer = setInterval(
      () => setRemaining(Math.max(0, initial - (performance.now() - received))),
      200,
    )
    return () => clearInterval(timer)
  }, [deadline, serverTime])
  const seconds = Math.ceil(remaining / 1000)
  useEffect(() => {
    if (soundId && seconds > 0 && seconds <= 5)
      gameAudio.play(`${soundId}/warning`, 'warning')
  }, [seconds, soundId])
  return (
    <span
      className="game-countdown"
      data-urgent={showUrgency && seconds > 0 && seconds <= 10}
    >
      <span key={seconds}>{seconds}</span>
    </span>
  )
}
