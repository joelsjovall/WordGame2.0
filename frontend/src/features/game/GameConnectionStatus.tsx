import { Link } from 'react-router'
import type { GameSynchronizationSnapshot } from '../../services/gameSynchronization.ts'

export function GameConnectionStatus({
  connection,
}: Pick<GameSynchronizationSnapshot, 'connection'>) {
  if (connection === 'connected')
    return <p role="status">Connected to match.</p>
  if (connection === 'connecting')
    return <p role="status">Connecting to match…</p>
  if (connection === 'recovering')
    return (
      <p role="status">
        Reconnecting to match… Your place and score are kept during the grace
        period. Timers continue on the server.
      </p>
    )
  return (
    <section>
      <p role="alert">
        This match is no longer available or your session has expired.
      </p>
      <Link to="/">Back to homepage</Link>
    </section>
  )
}
