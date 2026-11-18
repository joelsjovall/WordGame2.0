# Shared gameplay domain models

F004 defines the model foundation in `backend/WordGame.Domain`. The assembly references only the .NET runtime: no HTTP, persistence, frontend, or provider dependencies. Party Mode and Online Mode use the same types. Lobby readiness, guests, network connections, and matchmaking are separate concerns.

## Ownership and relationships

| Model | Responsibility and relationships |
| --- | --- |
| `Game` | Owns a fixed, ordered roster of 2-4 players, sequential rounds, and per-player scores. |
| `Player` | Immutable match participant ID and display name; not an authentication credential or connection. |
| `GameRound` | Owns category selection, highest bid, current turn, challenge, and round status. Created only through its game. |
| `Category` | Immutable stable string ID, display name, and Easy/Medium/Hard difficulty. |
| `AcceptedAnswer` / `CategoryDefinition` | Canonical answers and aliases in an immutable indexed category snapshot; see [category validation](category-validation.md). |
| `Bid` | Immutable bidder ID and positive required word count. |
| `Challenge` | Owns the challenged bid, caller, start/deadline, submissions, and outcome. A mutable challenge instance can belong to only one round. |
| `SubmittedWord` | Immutable bidder ID, display/normalized text, server-supplied timestamp, validation status, and canonical accepted-answer identity. |
| `PlayerTurn` | Immutable participant ID and positive sequence number within a round. |
| `PlayerScore` | Immutable participant ID and nonnegative points; used for totals and externally calculated round awards. |

IDs cannot be empty; required text cannot be blank. Display text is trimmed. Category IDs are provider-neutral strings; participant, game, round, and challenge IDs are GUIDs. Collections are read-only views, the input roster is copied, and value properties have no public setters.

F005 introduced normalization and cyclic roster lookup; see the [V1 migration review](v1-migration-review.md). F006 extends `WordNormalizer` to collapse internal whitespace as well as lowercase invariantly. `SubmittedWord.NormalizedText` uses that shared rule. `Game.GetNextPlayerId` remains a side-effect-free lookup rather than automatic turn progression.

## States and allowed transitions

| Object | Transition | Required condition |
| --- | --- | --- |
| Game | WaitingToStart -> InProgress | Start the first round with a roster member as starting player. |
| Game | InProgress -> InProgress | Start another round only after the current round is completed. |
| Game | InProgress -> Completed | At least one round exists and the final round is completed. |
| Game | WaitingToStart / InProgress -> Cancelled | Explicit cancellation; any unfinished round and pending challenge are cancelled too. |
| Round | CategorySelection -> Bidding | Current player selects a category. |
| Round | Bidding -> Challenge | Current caller challenges the highest bid; the bidder and caller differ. |
| Round | Challenge -> Completed | Its challenge has succeeded or failed. |
| Round | Any unfinished state -> Cancelled | The owning game is cancelled. |
| Challenge | Pending -> Succeeded / Failed | Accepted count agrees with the requested outcome. |
| Challenge | Pending -> Cancelled | Its owning round is cancelled. |

Terminal states cannot restart. State changes use guarded methods, not public status setters or a generic unrestricted transition method. Invalid inputs throw argument exceptions; invalid actions throw `InvalidOperationException`. The future application engine will translate failures into structured action results.

## Invariants

- Each game has 2-4 non-null participants with unique IDs. Ordered roster membership is stable for the match; disconnect state is not represented by deleting a participant.
- Round IDs cannot repeat within a game; round numbers increase from 1. Failed round creation leaves the game unchanged.
- Only the current roster member can select a category, bid, or initiate a challenge. Category selection locks once bidding starts. Bids are positive and strictly increase.
- Turn assignment requires another roster member and is restricted to category selection/bidding. It advances the sequence, but does not choose the next player. Beginning a challenge assigns the turn to the bidder. Terminal rounds retain their last turn as history.
- A challenge must refer to the current highest bid and arrive unresolved with no submissions. It cannot be attached to another round. Its deadline must follow its start.
- Submissions belong to the bidder, arrive while the challenge is pending, and use the half-open interval `[StartedAt, Deadline)`. Submission timestamps are supplied by trusted server code.
- Accepted submissions require a canonical answer ID. Repeated accepted canonical IDs are rejected; rejected/duplicate statuses can be recorded inside the time window and do not increase the accepted count. Canonical IDs use ordinal equality; normalization and alias mapping remain the validation service's responsibility.
- Success requires at least the bid's word count; failure requires fewer accepted words. Resolution is terminal. These are result-consistency checks, not timer orchestration.
- Scores begin at zero. A completed round can apply an externally calculated award batch once. Awards must reference roster members and contain at most one entry per player. All totals are checked before any are changed, so invalid input or integer overflow cannot partially apply a batch.

## Deliberately deferred

F013-F019 will supply engine orchestration, automatic turn progression and starting-player policy, timeout scheduling, category availability, submission orchestration using F006 validation, automatic challenge resolution, and match win conditions. There is no `GameEngine`, networking, or provider implementation here.

The domain does not read a clock, retrieve datasets, or calculate a points formula. Its category snapshot provides pure answer lookup; Application's F006 validator combines lookup with authoritative accepted history. Callers must be trusted backend code; these objects are not request DTOs. A future engine must serialize actions within a session; these mutable aggregates are not thread-safe.

F018 explicitly leaves the scoring formula unfinalized. The nonnegative integer score representation makes no choice about difficulty multipliers, winning targets, or actual award amounts. Confirm those rules in F018; penalties or negative totals would require revisiting this representation.

## Verification

`WordGame.Domain.Tests` covers model construction, both supported roster sizes and intermediate Party sizes, read-only ownership, valid and invalid transitions, turns, bids, challenge roles, timing boundaries, accepted-answer identity, cancellation, score replay protection, atomic overflow handling, and game isolation. Run it with:

```sh
dotnet test backend/WordGame.Domain.Tests --configuration Release
```
