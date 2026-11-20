import { useEffect, useState } from 'react'

export function SearchTimer({
  timestamp,
  countdown = false,
}: {
  timestamp: string
  countdown?: boolean
}) {
  const [now, setNow] = useState(Date.now)
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [])
  const seconds = Math.max(
    0,
    Math.floor(
      (countdown ? Date.parse(timestamp) - now : now - Date.parse(timestamp)) /
        1000,
    ),
  )
  return (
    <span aria-live="off">
      {String(Math.floor(seconds / 60)).padStart(2, '0')}:
      {String(seconds % 60).padStart(2, '0')}
    </span>
  )
}
