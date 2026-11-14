import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SLOW_TEST_TIMEOUT_MS } from '../test/timeouts'
import { Gallery } from './Gallery'

function section(name: string): HTMLElement {
  const heading = screen.getByRole('heading', { level: 2, name })
  return heading.closest('section') as HTMLElement
}

// Whichever test runs first pays for the gallery's first render, the slowest in the suite.
describe('Gallery', { timeout: SLOW_TEST_TIMEOUT_MS }, () => {
  it('shows every ticket status', () => {
    render(<Gallery />)

    for (const label of ['Open', 'In progress', 'Waiting on customer', 'Resolved', 'Closed']) {
      expect(within(section('Status')).getByText(label)).toBeInTheDocument()
    }
  })

  it('shows every ticket priority', () => {
    render(<Gallery />)

    for (const label of ['Low', 'Medium', 'High', 'Urgent']) {
      expect(within(section('Priority')).getByText(label)).toBeInTheDocument()
    }
  })

  it('shows a badge for each tone', () => {
    render(<Gallery />)

    for (const label of ['Agent', 'Admin', 'Internal']) {
      expect(within(section('Badge')).getByText(label)).toBeInTheDocument()
    }
  })

  it('shows a field with a hint and an invalid field with its error', () => {
    render(<Gallery />)

    expect(screen.getByRole('textbox', { name: 'Email' })).toHaveAccessibleDescription('Use the address you signed up with.')
    expect(screen.getByRole('textbox', { name: 'Title' })).toBeInvalid()
    expect(screen.getByRole('textbox', { name: 'Title' })).toHaveAccessibleDescription('Enter a title.')
  })

  it('shows a button per variant and a loading button', () => {
    render(<Gallery />)
    const buttons = within(section('Button'))

    for (const name of ['Create ticket', 'Cancel', 'Edit']) {
      expect(buttons.getByRole('button', { name })).toBeInTheDocument()
    }
    expect(buttons.getByRole('button', { name: 'Saving…' })).toHaveAttribute('aria-busy', 'true')
  })

  it('shows a loading region with its label', () => {
    render(<Gallery />)

    expect(screen.getByRole('status')).toHaveTextContent('Loading tickets…')
  })

  it('shows an empty state', () => {
    render(<Gallery />)

    expect(screen.getByRole('heading', { level: 3, name: 'No tickets yet' })).toBeInTheDocument()
  })
})
