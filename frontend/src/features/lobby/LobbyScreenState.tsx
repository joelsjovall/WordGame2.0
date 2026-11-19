import { Link } from 'react-router'

export function LobbyScreenState({ error }: { error: string }) {
  return (
    <main className="page party-lobby party-state" aria-busy={!error}>
      <Link to="/" className="party-back">
        ← WordGame
      </Link>
      <p className="party-eyebrow">Party Mode</p>
      <h1>Private lobby</h1>
      <section className="party-surface">
        {error ? (
          <>
            <h2>Unable to open this lobby</h2>
            <p role="alert">{error}</p>
            <p>Check the invitation or ask your host for a new one.</p>
            <Link to="/join">Join a lobby</Link>
          </>
        ) : (
          <>
            <p role="status">Loading lobby…</p>
            <div className="party-skeleton" aria-hidden="true">
              <span />
              <span />
              <span />
            </div>
          </>
        )}
      </section>
    </main>
  )
}
