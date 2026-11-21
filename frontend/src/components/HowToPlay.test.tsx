// @vitest-environment jsdom
import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, test, vi } from 'vitest'
import { HowToPlay } from './HowToPlay.tsx'

test('opens rules and explains both practice decisions without gameplay actions', async () => {
  // jsdom lacks the native dialog methods; browser tests cover focus and Escape.
  const show = vi.fn(function (this: HTMLDialogElement) {
    this.setAttribute('open', '')
  })
  const close = vi.fn(function (this: HTMLDialogElement) {
    this.removeAttribute('open')
  })
  Object.defineProperties(HTMLDialogElement.prototype, {
    showModal: { configurable: true, value: show },
    close: { configurable: true, value: close },
  })
  const user = userEvent.setup()
  render(<HowToPlay />)
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'How to Play' }))
  const rules = within(screen.getByRole('dialog', { name: 'How to Play' }))
  expect(show).toHaveBeenCalledOnce()
  expect(rules.getByText(/Turns and timers/)).toBeVisible()
  expect(rules.getByLabelText('Scoring example')).toHaveTextContent(
    'bidder 3 × 2 = 6; caller 50',
  )
  expect(rules.getByText(/Tied top scorers/)).toBeVisible()
  await user.click(rules.getByRole('button', { name: 'Raise to 6' }))
  expect(rules.getByRole('status')).toHaveTextContent(
    'now you claim you can name 6',
  )
  await user.click(rules.getByRole('button', { name: "Call Bo's bluff" }))
  expect(rules.getByRole('status')).toHaveTextContent(
    'Correct! Bo must prove the bid of 5',
  )
  await user.click(rules.getByRole('button', { name: 'Back to play' }))
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'How to Play' }))
  fireEvent.click(screen.getByRole('button', { name: 'Close instructions' }))
  expect(close).toHaveBeenCalledTimes(2)
})
