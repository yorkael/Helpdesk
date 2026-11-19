import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { EmptyState } from './EmptyState'

describe('EmptyState', () => {
  it('shows its title as a level 2 heading by default', () => {
    render(<EmptyState title="No tickets yet" />)

    expect(screen.getByRole('heading', { level: 2, name: 'No tickets yet' })).toBeInTheDocument()
  })

  it('shows its description and action', () => {
    render(<EmptyState title="No tickets yet" description="Tickets you create appear here." action={<a href="/new">New ticket</a>} />)

    expect(screen.getByText('Tickets you create appear here.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'New ticket' })).toBeInTheDocument()
  })

  it('uses the heading level it is given', () => {
    render(<EmptyState title="No comments" headingLevel={3} />)

    expect(screen.getByRole('heading', { level: 3, name: 'No comments' })).toBeInTheDocument()
  })
})
