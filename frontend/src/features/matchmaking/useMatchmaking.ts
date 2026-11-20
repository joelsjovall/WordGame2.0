import { useEffect, useRef, useState } from 'react'
import {
  connectMatchmaking,
  emptyMatchmaking,
} from '../../services/matchmakingConnection.ts'

export function useMatchmaking() {
  const [snapshot, setSnapshot] = useState(emptyMatchmaking)
  const connection = useRef<ReturnType<typeof connectMatchmaking> | null>(null)
  useEffect(() => {
    const current = connectMatchmaking(setSnapshot)
    connection.current = current
    return () => {
      connection.current = null
      current.stop()
    }
  }, [])
  return {
    ...snapshot,
    join: (name: string) => connection.current?.join(name),
    cancel: () => connection.current?.cancel(),
    respond: (action: 'accept' | 'decline') =>
      connection.current?.respond(action),
  }
}
