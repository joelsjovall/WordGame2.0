// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, test, vi } from 'vitest'
import { Button, TextField } from './ui.tsx'

test('a busy action keeps its accessible name and prevents duplicate activation', async () => {
  const action = vi.fn()
  const view = render(
    <Button busy onClick={action}>
      Accept match
    </Button>,
  )
  const button = screen.getByRole('button', { name: 'Accept match' })
  expect(button).toHaveAttribute('aria-busy', 'true')
  await userEvent.setup().click(button)
  expect(action).not.toHaveBeenCalled()
  view.rerender(<Button onClick={action}>Accept match</Button>)
  await userEvent.setup().click(button)
  expect(action).toHaveBeenCalledOnce()
})
test('shared text fields retain labels and error descriptions', () => {
  render(
    <>
      <TextField
        label="Display name"
        aria-invalid="true"
        aria-describedby="name-error"
      />
      <p id="name-error">Enter a name.</p>
    </>,
  )
  expect(
    screen.getByRole('textbox', { name: 'Display name' }),
  ).toHaveAccessibleDescription('Enter a name.')
})
