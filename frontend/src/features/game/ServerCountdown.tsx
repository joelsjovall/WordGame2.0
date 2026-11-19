import { useEffect, useState } from 'react'

export function ServerCountdown({
  deadline,
  serverTime,
}: {
  deadline: string
  serverTime: string
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
  return <span>{Math.ceil(remaining / 1000)}</span>
}
