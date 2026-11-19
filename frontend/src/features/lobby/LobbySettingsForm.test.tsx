// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, test, vi } from 'vitest'
import { LobbySettingsForm } from './LobbySettingsForm.tsx'
import { lobbyFixture as lobby } from '../../../tests/fixtures/lobby.ts'

test('host edits supported settings and selects available difficulty levels', async () => {
  const save = vi.fn().mockResolvedValue(undefined)
  const user = userEvent.setup()
  render(<LobbySettingsForm lobby={lobby} busy={false} save={save} />)
  await user.clear(screen.getByLabelText('Total rounds'))
  await user.type(screen.getByLabelText('Total rounds'), '12')
  await user.clear(screen.getByLabelText('Challenge time (seconds)'))
  await user.type(screen.getByLabelText('Challenge time (seconds)'), '75')
  await user.click(screen.getByLabelText('easy'))
  await user.click(screen.getByLabelText('medium'))
  await user.click(screen.getByRole('button', { name: 'Save settings' }))
  expect(save).toHaveBeenCalledWith({
    ...lobby.settings,
    roundCount: 12,
    challengeSeconds: 75,
    allowedDifficulties: ['hard'],
  })
})
test('empty difficulties and pending writes disable saving; round count can remain automatic', async () => {
  const save = vi.fn()
  const user = userEvent.setup()
  const view = render(
    <LobbySettingsForm lobby={lobby} busy={false} save={save} />,
  )
  for (const difficulty of ['easy', 'medium', 'hard'])
    await user.click(screen.getByLabelText(difficulty))
  expect(screen.getByRole('button', { name: 'Save settings' })).toBeDisabled()
  expect(screen.getByLabelText('Maximum players')).toHaveAttribute('min', '2')
  view.rerender(<LobbySettingsForm lobby={lobby} busy={true} save={save} />)
  expect(screen.getByLabelText('Challenge time (seconds)')).toBeDisabled()
  expect(save).not.toHaveBeenCalled()
})
