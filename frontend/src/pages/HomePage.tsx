export function HomePage() {
  return (
    <main className="page home-page">
      <p className="eyebrow">Think fast. Bid higher. Call the bluff.</p>
      <h1>WordGame 2.0</h1>
      <Card>
        <p>A multiplayer word-bidding and bluffing game.</p>
        <p>Play with friends or find a new rival.</p>
        <p>
          <Link className="wg-button" data-variant="primary" to="/online">
            Play Online
            <svg className="wg-icon" viewBox="0 0 24 24" aria-hidden="true">
              <path d="M5 12h14m-6-6 6 6-6 6" />
            </svg>
          </Link>
        </p>
        <p>
          <Link to="/lobbies/new">Create a private lobby</Link>
        </p>
        <p>
          <Link to="/join">Join a private lobby</Link>
        </p>
      </Card>
    </main>
  )
}
import { Link } from 'react-router'
import { Card } from '../components/ui.tsx'
