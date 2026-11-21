import { useEffect, useState, type RefObject } from 'react'

// VisualViewport also shrinks for browser zoom; compact layout is useful in either case.
export function useCompactEntry(entry: RefObject<HTMLElement | null>) {
  const [compact, setCompact] = useState(false)
  useEffect(() => {
    const viewport = window.visualViewport
    let frame = 0
    const update = () => {
      cancelAnimationFrame(frame)
      frame = requestAnimationFrame(() => {
        const focused = document.activeElement
        const editing =
          focused instanceof HTMLInputElement &&
          entry.current?.contains(focused)
        const short = (viewport?.height ?? window.innerHeight) < 480
        setCompact(Boolean(editing && short))
        if (editing && short)
          entry.current?.scrollIntoView({ block: 'start', behavior: 'instant' })
      })
    }
    viewport?.addEventListener('resize', update)
    window.addEventListener('resize', update)
    document.addEventListener('focusin', update)
    document.addEventListener('focusout', update)
    return () => {
      cancelAnimationFrame(frame)
      viewport?.removeEventListener('resize', update)
      window.removeEventListener('resize', update)
      document.removeEventListener('focusin', update)
      document.removeEventListener('focusout', update)
    }
  }, [entry])
  return compact
}
