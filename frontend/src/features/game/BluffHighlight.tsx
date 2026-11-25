import type { GameState } from '../../services/api/games.ts'

export function BluffHighlight({
  highlight,
}: {
  highlight: NonNullable<GameState['result']>['bestBluffCall']
}) {
  if (!highlight) return null
  return (
    <p className="game-bluff-highlight">
      <strong>Best bluff call:</strong> {highlight.callerName} caught{' '}
      {highlight.bidderName}&apos;s {highlight.wordCount}-word bid in{' '}
      {highlight.categoryName}.
    </p>
  )
}
