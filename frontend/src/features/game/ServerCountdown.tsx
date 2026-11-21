import { useEffect, useState } from 'react'

export function ServerCountdown({
  deadline,
  serverTime,
  showUrgency = false,
}: {
  deadline: string
  serverTime: string
  showUrgency?: boolean
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
  return (
    <span
      className="game-countdown"
      data-urgent={showUrgency && seconds > 0 && seconds <= 10}
    >
      <span key={seconds}>{seconds}</span>
    </span>
  )
}
