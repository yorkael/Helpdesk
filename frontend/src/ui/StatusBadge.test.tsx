import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TICKET_STATUSES } from '../lib/ticket'
import { StatusBadge } from './StatusBadge'

describe('StatusBadge', () => {
  it.each(TICKET_STATUSES)('marks %s with its API value in data-status', (status) => {
    const { container } = render(<StatusBadge status={status} />)

    expect(container.firstElementChild).toHaveAttribute('data-status', status)
  })

  it.each(TICKET_STATUSES)('names %s in text, never by color alone', (status) => {
    const { container } = render(<StatusBadge status={status} />)

    expect(container.firstElementChild?.textContent?.trim()).not.toBe('')
  })

  it.each([
    ['Open', 'Open'],
    ['InProgress', 'In progress'],
    ['WaitingOnCustomer', 'Waiting on customer'],
    ['Resolved', 'Resolved'],
    ['Closed', 'Closed'],
  ] as const)('shows %s as "%s"', (status, label) => {
    const { container } = render(<StatusBadge status={status} />)

    expect(container.firstElementChild?.textContent).toBe(label)
  })

  it.each([
    ['Open', 'bg-status-open-bg', 'text-status-open-fg'],
    ['InProgress', 'bg-status-in-progress-bg', 'text-status-in-progress-fg'],
    ['WaitingOnCustomer', 'bg-status-waiting-on-customer-bg', 'text-status-waiting-on-customer-fg'],
    ['Resolved', 'bg-status-resolved-bg', 'text-status-resolved-fg'],
    ['Closed', 'bg-status-closed-bg', 'text-status-closed-fg'],
  ] as const)('colors %s with %s and %s', (status, background, text) => {
    const { container } = render(<StatusBadge status={status} />)

    expect(container.firstElementChild).toHaveClass(background, text)
  })
})
