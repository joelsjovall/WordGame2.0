import { useRef, useState } from 'react'
import { Button } from './ui.tsx'
import './how-to-play.css'

export function HowToPlay() {
  const dialog = useRef<HTMLDialogElement>(null)
  const [answer, setAnswer] = useState<'raise' | 'call' | null>(null)

  return (
    <>
      <Button variant="secondary" onClick={() => dialog.current?.showModal()}>
        How to Play
      </Button>
      <dialog ref={dialog} className="how-to-play" aria-label="How to Play">
        <header className="how-to-play-heading">
          <h2>How to Play</h2>
          <Button variant="secondary" onClick={() => dialog.current?.close()}>
            Close instructions
          </Button>
        </header>
        <p>
          Outbid your rivals, prove your words, or catch a bluff. Collect the
          highest total score to win.
        </p>
        <p role="note">
          Opening these instructions keeps you in your match. Turns and timers
          continue while you read. Close them to return to play.
        </p>
        <ol className="how-to-play-steps">
          <li>
            <h3>Choose a category</h3>
            <p>
              The active player chooses from the available categories. Starters
              rotate each round. If the selection timer runs out, the server
              chooses a category automatically.
            </p>
          </li>
          <li>
            <h3>Bid, raise, or call</h3>
            <p>
              The category selector opens with a positive whole-number bid: how
              many different answers they can name. On your turn, bid more than
              the current highest bid or call that bidder&apos;s bluff. You
              cannot call your own bluff.
            </p>
            <div className="how-to-play-example" aria-label="Bidding example">
              <p>Ana: &ldquo;I can name 3 European capitals.&rdquo;</p>
              <p>Bo: &ldquo;I can name 5.&rdquo;</p>
              <p>
                Ana calls bluff. Bo must now name 5, not Ana&apos;s earlier 3.
              </p>
            </div>
          </li>
          <li>
            <h3>Prove the bid against the clock</h3>
            <p>
              Only the challenged bidder submits answers, one at a time. Reach
              the bid before the countdown ends to succeed. Otherwise the
              challenge fails. The usual challenge lasts 30 seconds; follow the
              timer shown in your match.
            </p>
            <p>
              The server checks each answer against the category&apos;s accepted
              answers and listed alternative spellings. Capitalization and extra
              spaces do not matter. Typos or unlisted spellings are not
              accepted. Each distinct answer counts once, including its
              alternative spellings. Invalid and duplicate answers earn nothing;
              keep trying while time remains.
            </p>
            <div className="how-to-play-example" aria-label="Answer example">
              <p>Stockholm → Accepted · 1 answer</p>
              <p>STOCKHOLM → Duplicate · still 1 answer</p>
              <p>Banana → Invalid for European capitals · still 1 answer</p>
            </div>
          </li>
          <li>
            <h3>Score the round</h3>
            <p>
              The bidder earns points for every accepted unique answer, even if
              they fail. Default points per word: Easy 1, Medium 2, Hard 3.
              Success adds a 50-point bonus to the bidder. Failure awards that
              bonus to the caller instead. Other players score nothing that
              round. There are no negative points.
            </p>
            <div className="how-to-play-example" aria-label="Scoring example">
              <p>Medium category, bid of 5, using default scoring:</p>
              <p>5 accepted in time: bidder 5 × 2 + 50 = 60; caller 0.</p>
              <p>Only 3 accepted: bidder 3 × 2 = 6; caller 50.</p>
            </div>
            <p>
              Server settings can change word values and the outcome bonus.
              Round results show the actual points awarded. The next round
              starts automatically after a short results break.
            </p>
          </li>
          <li>
            <h3>Win the match</h3>
            <p>
              After the scheduled rounds, the highest total score wins. Tied top
              scorers share the win; there are no extra tiebreak rounds.
            </p>
          </li>
        </ol>
        <section aria-labelledby="how-modes">
          <h3 id="how-modes">Two modes, the same rules</h3>
          <p>
            <strong>Party:</strong> 2–4 friends in a private lobby. Join by code
            or invitation, ready up, and let the host start. The host can set
            the match length; the default is two rounds per player. Return to
            the lobby for a rematch.
          </p>
          <p>
            <strong>Online:</strong> automatic 1v1 matchmaking without mandatory
            registration. Both players accept the match, then play six rounds.
            You can request a rematch or find a new opponent afterward.
            Forfeiting or failing to reconnect before the grace period ends can
            lose the match.
          </p>
        </section>
        <section aria-labelledby="how-practice">
          <h3 id="how-practice">Try a decision</h3>
          <p>
            Bo bids 5. It&apos;s your turn. You think Bo cannot name 5. What do
            you do?
          </p>
          <div className="how-to-play-actions">
            <Button variant="secondary" onClick={() => setAnswer('raise')}>
              Raise to 6
            </Button>
            <Button variant="secondary" onClick={() => setAnswer('call')}>
              Call Bo&apos;s bluff
            </Button>
          </div>
          <p role="status">
            {answer === 'call'
              ? 'Correct! Bo must prove the bid of 5. If Bo fails, you get the outcome bonus.'
              : answer === 'raise'
                ? 'A legal raise, but now you claim you can name 6. Call bluff to make Bo prove 5.'
                : 'Choose an answer to see what happens. This practice does not affect your match.'}
          </p>
        </section>
        <Button onClick={() => dialog.current?.close()}>Back to play</Button>
      </dialog>
    </>
  )
}
