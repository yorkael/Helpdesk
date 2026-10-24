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

  it.each([
    ['Low', ['bg-priority-low-bar', 'bg-priority-bar-off', 'bg-priority-bar-off', 'bg-priority-bar-off']],
    ['Medium', ['bg-priority-medium-bar', 'bg-priority-medium-bar', 'bg-priority-bar-off', 'bg-priority-bar-off']],
    ['High', ['bg-priority-high-bar', 'bg-priority-high-bar', 'bg-priority-high-bar', 'bg-priority-bar-off']],
    ['Urgent', ['bg-priority-urgent-bar', 'bg-priority-urgent-bar', 'bg-priority-urgent-bar', 'bg-priority-urgent-bar']],
  ] as const)('colors the active bars of %s with its bar color and the rest with the off color', (priority, expected) => {
    const { container } = render(<PriorityIndicator priority={priority} />)

    expect(bars(container).map((bar) => [...bar.classList].find((name) => name.startsWith('bg-')))).toEqual(expected)
  })
})
