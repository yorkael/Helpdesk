import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TICKET_PRIORITIES } from '../lib/ticket'
import { PriorityIndicator } from './PriorityIndicator'

function bars(container: HTMLElement): Element[] {
  return [...container.querySelectorAll('[data-active]')]
}

describe('PriorityIndicator', () => {
  it.each(TICKET_PRIORITIES)('hides the bars of %s from assistive technology', (priority) => {
    const { container } = render(<PriorityIndicator priority={priority} />)

    expect(bars(container)[0].parentElement).toHaveAttribute('aria-hidden', 'true')
  })

  it.each(TICKET_PRIORITIES)('draws four bars of growing height for %s', (priority) => {
    const { container } = render(<PriorityIndicator priority={priority} />)

    expect(bars(container).map((bar) => ['h-1', 'h-2', 'h-3', 'h-4'].find((height) => bar.classList.contains(height)))).toEqual([
      'h-1',
      'h-2',
      'h-3',
      'h-4',
    ])
  })

  it.each([
    ['Low', 'Low'],
    ['Medium', 'Medium'],
    ['High', 'High'],
    ['Urgent', 'Urgent'],
  ] as const)('names %s in text as "%s"', (priority, label) => {
    const { container } = render(<PriorityIndicator priority={priority} />)

    expect(container.firstElementChild?.textContent).toBe(label)
  })

  it.each([
    ['Low', '1'],
    ['Medium', '2'],
    ['High', '3'],
    ['Urgent', '4'],
  ] as const)('exposes %s as data-level %s', (priority, level) => {
    const { container } = render(<PriorityIndicator priority={priority} />)

    expect(container.firstElementChild).toHaveAttribute('data-level', level)
  })

  it.each([
    ['Low', ['true', 'false', 'false', 'false']],
    ['Medium', ['true', 'true', 'false', 'false']],
    ['High', ['true', 'true', 'true', 'false']],
    ['Urgent', ['true', 'true', 'true', 'true']],
  ] as const)('lights the first bars for %s, in order', (priority, expected) => {
    const { container } = render(<PriorityIndicator priority={priority} />)

    expect(bars(container).map((bar) => bar.getAttribute('data-active'))).toEqual(expected)
  })

  // Checks each bar's color against its own state, not its position: which bars are lit is the ordering test's job.
  it.each([
    ['Low', 'bg-priority-low-bar'],
    ['Medium', 'bg-priority-medium-bar'],
    ['High', 'bg-priority-high-bar'],
    ['Urgent', 'bg-priority-urgent-bar'],
  ] as const)('colors the active bars of %s with %s and the rest with the off color', (priority, barColor) => {
    const { container } = render(<PriorityIndicator priority={priority} />)
    const allBars = bars(container)

    expect(allBars).toHaveLength(4)
    for (const bar of allBars) {
      expect(bar).toHaveClass(bar.getAttribute('data-active') === 'true' ? barColor : 'bg-priority-bar-off')
    }
  })
})
