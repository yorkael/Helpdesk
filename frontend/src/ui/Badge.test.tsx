import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { Badge } from './Badge'

describe('Badge', () => {
  it.each(['neutral', 'primary', 'internal'] as const)('shows its text with the %s tone', (tone) => {
    render(<Badge tone={tone}>Agent</Badge>)

    expect(screen.getByText('Agent')).toBeInTheDocument()
  })

  it('draws a dot only for the internal tone', () => {
    const { container, rerender } = render(<Badge tone="internal">Internal</Badge>)

    expect(container.querySelector('[data-dot]')).toBeInTheDocument()

    rerender(<Badge tone="neutral">Agent</Badge>)

    expect(container.querySelector('[data-dot]')).not.toBeInTheDocument()
  })

  it('hides the internal dot from assistive technology', () => {
    const { container } = render(<Badge tone="internal">Internal</Badge>)

    expect(container.querySelector('[data-dot]')).toHaveAttribute('aria-hidden', 'true')
  })

  it.each([
    ['neutral', ['border-line-strong', 'bg-paper', 'text-muted']],
    ['primary', ['border-transparent', 'bg-primary-soft', 'text-primary']],
    ['internal', ['border-internal-line', 'bg-internal-bg', 'text-ink']],
  ] as const)('colors the %s tone with %s', (tone, classes) => {
    const { container } = render(<Badge tone={tone}>Agent</Badge>)

    expect(container.firstElementChild).toHaveClass(...classes)
  })
})
