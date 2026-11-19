export function HomePage() {
  return (
    <main className="page">
      <p className="eyebrow">Think fast. Bid higher. Call the bluff.</p>
      <h1>WordGame 2.0</h1>
      <p>A multiplayer word-bidding and bluffing game.</p>
      <p>Party Mode and Online Mode are coming soon.</p>
      <p>
        <Link to="/join">Join a private lobby</Link>
      </p>
    </main>
  )
}
import { Link } from 'react-router'
