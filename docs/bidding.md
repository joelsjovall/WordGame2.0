# Bidding and bluff calls

F016 completes the bidding boundary and verifies the shared mechanics introduced by the Domain, lifecycle, turn, and category work. Party and Online matches use the same `GameEngine.PlaceBidAsync` and `CallBluffAsync` operations, Domain guards, and atomic session store.

## Initial state and raises

A round enters Bidding after provider-backed category selection. `HighestBid` and `Challenge` are initially null; there is no implicit opening bid. The category selector remains active to make the opening claim. Both bidding operations require the selected answer snapshot to belong to the current round, so a low-level metadata-only mutation cannot bypass category validation.

An opening bid may be any positive Int32 word count. Each subsequent bid must strictly exceed the highest bid. Zero/negative counts, equal/lower raises, nonmembers, unavailable players, and out-of-turn requests are rejected without changing the highest bid, turn, timestamps, or revision.

`GameRound.HighestBid` is an immutable `Bid(PlayerId, WordCount)` identifying the latest accepted claimant and amount. Each valid bid records that value and advances once to the next eligible player in the [stable turn order](turn-management.md). Difficulty modifiers do not change the minimum opening bid or required raise.

A bid is a bluffable claim, not a guarantee that the dataset contains that many answers. The engine does not cap claims at the accepted-answer count or disclose that count. At Int32.MaxValue, another raise is impossible; the next eligible player can still call bluff without an implicit increment or integer overflow.

## Who may call bluff

Only the eligible player currently holding the bidding turn may challenge the current highest bid. A bid must already exist, its bidder must still be eligible under the disconnect policy, and a player cannot challenge their own bid. Other participants wait for their turn; nonmembers cannot initiate a challenge.

The call captures the **current** highest Bid in a new server-generated Challenge. The highest bidder becomes the challenged player and active participant; the caller becomes ChallengerId. The server supplies the challenge ID and current start time, with a positive challenge duration supplied by trusted backend policy. The deadline follows the start. Client action DTOs supply neither actor identity nor authoritative timestamps/duration.

For the issue's example: A bids 3, B raises to 5, A calls bluff; B must attempt 5 words. The challenge refers to B's 5-word bid, not A's earlier claim. Challenge starts Pending with no submissions.

The round then enters Challenge. Neither participant can place another bid or create another challenge, including the bidder now holding the challenge turn. Word submission, timed challenge orchestration, and scoring remain their own issues. No bidding-timeout rule or additional game HTTP endpoint is introduced by F016.

## Rejections, replays, and races

Both methods use `PlayerActionContext(ActionId, ExpectedVersion)` and the shared command executor. See [turn management](turn-management.md#repeated-and-stale-commands) for exact stale/duplicate behavior and the original-response replay limitation.

Bid recording plus turn advancement commit together. Challenge creation plus phase/turn transition also commit together. A rejected action cannot partially update any of those fields or consume an action ID. A replay with a new revision and altered bid amount still cannot reuse an accepted ID.

Competing raises and bluff calls compare revisions under the same per-session gate. One request can commit against a given version; the other returns StaleState and must retrieve authoritative state. A committed raise preserves Bidding and updates the claimant; a committed bluff preserves the challenged bid and locks the phase. Repeated calls cannot create multiple challenges, reset deadlines, or advance the turn again. Separate games remain isolated even when they share the same roster IDs.

## Verification

Focused Application tests cover initial state, opening bids and raises across 2–4-player rosters, the issue's example, invalid/equal/lower/maximal bids, membership/eligibility/turn checks, missing category snapshots, self-challenge prevention, challenge phase locking, and bid/bluff replay guards. Real-store tests cover competing raise/bluff requests, twenty concurrent bluff retries creating one challenge, and independent simultaneous matches. Existing domain and turn tests remain part of the suite; see [testing](testing.md) for commands.
