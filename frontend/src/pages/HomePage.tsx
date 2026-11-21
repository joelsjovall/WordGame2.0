import { Link } from 'react-router'
import { Badge, Card } from '../components/ui.tsx'
import { HowToPlay } from '../components/HowToPlay.tsx'
import './home.css'

export function HomePage() {
  return (
    <main className="page home-page">
      <header className="home-bar">
        <div className="home-brand">
          <span className="home-brand-tile" aria-hidden="true">
            W
          </span>
          <h1>
            WordGame <span>2.0</span>
          </h1>
        </div>
        <HowToPlay />
      </header>
      <section className="home-hero" aria-label="A game of words and nerve">
        <div>
          <p className="eyebrow">A game of words. A test of nerve.</p>
          <h2>
            Think fast.
            <br />
            <em>Bid higher.</em>
            <br />
            Call the bluff.
          </h2>
          <p className="home-lede">
            How many words can you really name? Pick a category, outbid your
            rivals, and back up your boldest claims before time runs out.
          </p>
          <p className="home-invitation">
            Play with friends or find a new rival.
          </p>
        </div>
        <aside className="home-table" aria-label="An example bluff">
          <span className="home-example-label">At the table</span>
          <p className="home-category">European capitals</p>
          <blockquote>
            &ldquo;I can name <strong>8.</strong>
            <br />
            Can you?&rdquo;
          </blockquote>
          <div className="home-letter-tiles" aria-hidden="true">
            {'BLUFF'.split('').map((letter, index) => (
              <span key={index}>{letter}</span>
            ))}
          </div>
          <p>Raise the bid. Or call their bluff.</p>
          <span className="home-example-label">
            One bold claim changes everything.
          </span>
        </aside>
      </section>
      <section className="home-modes" aria-label="Choose your game mode">
        <Card className="home-mode" aria-labelledby="party-mode-title">
          <div className="home-mode-heading">
            <Badge>Party Mode</Badge>
            <span>2&ndash;4 players</span>
          </div>
          <h2 id="party-mode-title">
            Your people.
            <br />
            Your bragging rights.
          </h2>
          <p>
            Make a private lobby, share the code, and challenge your friends.
            You set the match; they bring the bluffs.
          </p>
          <Link className="wg-button" data-variant="primary" to="/lobbies/new">
            Play with Friends <HomeArrow />
          </Link>
          <Link className="home-join" to="/join">
            Join a private lobby
          </Link>
        </Card>
        <Card
          className="home-mode home-mode-online"
          aria-labelledby="online-mode-title"
        >
          <div className="home-mode-heading">
            <Badge tone="warning">Online Mode</Badge>
            <span>1 vs 1</span>
          </div>
          <h2 id="online-mode-title">
            A new rival.
            <br />
            The same big stakes.
          </h2>
          <p>
            Get matched with another player for a six-round showdown. No lobby
            to organize. Just you, your words, and your nerve.
          </p>
          <Link className="wg-button" data-variant="secondary" to="/online">
            Play Online <HomeArrow />
          </Link>
          <p className="home-mode-note">No registration required.</p>
        </Card>
      </section>
      <section
        className="home-how"
        id="how-to-play"
        aria-labelledby="how-title"
      >
        <p className="eyebrow">Simple rules. Very bold decisions.</p>
        <h2 id="how-title">How to Play</h2>
        <ol>
          <li>
            <strong>Pick a category</strong>
            <span>Find your territory, from capitals to cars.</span>
          </li>
          <li>
            <strong>Make your bid</strong>
            <span>
              Say how many valid words you can name. Rivals raise the bid or
              call a bluff.
            </span>
          </li>
          <li>
            <strong>Beat the clock</strong>
            <span>
              The challenged bidder names their words before time runs out.
            </span>
          </li>
          <li>
            <strong>Take the points</strong>
            <span>
              Prove the bid to score, or catch a bluff. The highest total wins.
            </span>
          </li>
        </ol>
      </section>
      <footer className="home-footer">
        No downloads. No registration. Just one more round.
      </footer>
    </main>
  )
}

function HomeArrow() {
  return (
    <svg className="wg-icon" viewBox="0 0 24 24" aria-hidden="true">
      <path d="M6 18 18 6M6 6h12v12" />
    </svg>
  )
}
