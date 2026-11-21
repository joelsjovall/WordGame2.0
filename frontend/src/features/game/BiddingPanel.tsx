import { useState } from 'react'
import { Button, Card, TextField } from '../../components/ui.tsx'
import type { GamePanelProps } from './gameTypes.ts'

export function BiddingPanel({ state, playerId, busy, send }: GamePanelProps) {
  const round = state.round!
  const minimum = (round.highestBid?.wordCount ?? 0) + 1
  const [bid, setBid] = useState(String(minimum))
  const value = Number(bid)
  const valid =
    Number.isInteger(value) && value >= minimum && value <= 2147483647
  const turn = round.activePlayerId === playerId
  const activeName =
    state.players.find((player) => player.id === round.activePlayerId)
      ?.displayName ?? 'Another player'
  const bidderName = state.players.find(
    (player) => player.id === round.highestBid?.playerId,
  )?.displayName
  return (
    <Card className="game-panel" aria-labelledby="bidding-title">
      <p className="eyebrow">Raise the stakes</p>
      <h2 id="bidding-title">Bidding</h2>
      <div className="game-bid">
        <strong>{round.highestBid?.wordCount ?? '—'}</strong>
        <div>
          <p>
            {round.highestBid
              ? `Highest bid: ${round.highestBid.wordCount} words`
              : 'No bid yet.'}
          </p>
          {bidderName && <p>Bid by {bidderName}</p>}
        </div>
      </div>
      {turn ? (
        <>
          <form
            onSubmit={(event) => {
              event.preventDefault()
              if (!busy && valid) send({ type: 'placeBid', wordCount: value })
            }}
          >
            <TextField
              label="Your bid"
              id="word-bid"
              type="number"
              inputMode="numeric"
              min={minimum}
              max={2147483647}
              required
              value={bid}
              disabled={busy}
              aria-describedby="bid-help"
              onChange={(event) => setBid(event.target.value)}
            />
            <p id="bid-help" className="game-hint">
              Bid at least {minimum} {minimum === 1 ? 'word' : 'words'}.
              {round.highestBid &&
                round.highestBid.playerId !== playerId &&
                ' Or challenge the highest bid.'}
            </p>
            <Button type="submit" disabled={busy || !valid}>
              Place bid
            </Button>
          </form>
          {round.highestBid && round.highestBid.playerId !== playerId && (
            <div className="game-bluff">
              <p>Think they are bluffing? Make them prove it.</p>
              <Button
                variant="secondary"
                disabled={busy}
                onClick={() => send({ type: 'callBluff' })}
              >
                Call bluff
              </Button>
            </div>
          )}
        </>
      ) : (
        <p role="status">{activeName} is bidding. Watch the stakes rise.</p>
      )}
    </Card>
  )
}
