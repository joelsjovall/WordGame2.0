import { useState } from 'react'
import { Button, Card, TextField } from '../../components/ui.tsx'
import type { GamePanelProps } from './gameTypes.ts'

export function SealedBiddingPanel({
  state,
  playerId,
  busy,
  send,
}: GamePanelProps) {
  const round = state.round!
  const [bid, setBid] = useState('1')
  const [risk, setRisk] = useState(false)
  const count = Number(bid)
  const submitted = round.sealedBidSubmitterIds?.includes(playerId) ?? false
  return (
    <Card className="game-panel" aria-labelledby="sealed-title">
      <p className="eyebrow">Secret finale</p>
      <h2 id="sealed-title">Hidden opening bids</h2>
      <p>
        Category: {round.category?.name}. Everyone bids once in secret. Bids are
        revealed only after every player has submitted.
      </p>
      <p role="status">
        {round.sealedBidSubmitterIds?.length ?? 0} of {state.players.length}{' '}
        bids submitted.
      </p>
      {submitted ? (
        <p>Your bid is locked in. Waiting for the other players.</p>
      ) : (
        <form
          onSubmit={(event) => {
            event.preventDefault()
            if (
              !busy &&
              Number.isInteger(count) &&
              count > 0 &&
              count <= 2147483647
            )
              send({ type: 'submitSealedBid', wordCount: count, risk })
          }}
        >
          <TextField
            label="Your hidden bid"
            id="sealed-word-bid"
            type="number"
            inputMode="numeric"
            min={1}
            max={2147483647}
            required
            value={bid}
            disabled={busy}
            onChange={(event) => setBid(event.target.value)}
          />
          <label className="game-risk-bid">
            <input
              type="checkbox"
              checked={risk}
              disabled={busy}
              onChange={(event) => setRisk(event.target.checked)}
            />
            Risk bid: double bonus at stake.
          </label>
          <Button
            type="submit"
            busy={busy}
            disabled={
              busy ||
              !Number.isInteger(count) ||
              count < 1 ||
              count > 2147483647
            }
          >
            Lock in bid
          </Button>
        </form>
      )}
    </Card>
  )
}
