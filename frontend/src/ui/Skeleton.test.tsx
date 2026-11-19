import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { LoadingRegion, Skeleton } from './Skeleton'

describe('Skeleton', () => {
  it('takes its size from className', () => {
    const { container } = render(<Skeleton className="h-4 w-32" />)

    expect(container.firstElementChild).toHaveClass('h-4', 'w-32')
  })

  it('has no role of its own', () => {
    const { container } = render(<Skeleton />)

    expect(container.firstElementChild).not.toHaveAttribute('role')
  })

  it('is hidden from assistive technology', () => {
    const { container } = render(<Skeleton />)

    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true')
  })
})

describe('LoadingRegion', () => {
  it('renders its skeletons', () => {
    const { container } = render(
      <LoadingRegion label="Loading tickets…">
        <Skeleton />
        <Skeleton />
      </LoadingRegion>,
    )

    expect(container.querySelectorAll('.animate-pulse')).toHaveLength(2)
  })

  it('exposes a single status with its label', () => {
    const { getAllByRole } = render(
      <LoadingRegion label="Loading tickets…">
        <Skeleton />
        <Skeleton />
      </LoadingRegion>,
    )
    const statuses = getAllByRole('status')

    expect(statuses).toHaveLength(1)
    expect(statuses[0]).toHaveTextContent('Loading tickets…')
  })
})
