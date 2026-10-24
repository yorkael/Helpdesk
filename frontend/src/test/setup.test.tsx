import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

describe('test environment', () => {
  it('renders into jsdom with the jest-dom matchers', () => {
    render(<p>Rendered</p>)

    expect(screen.getByText('Rendered')).toBeInTheDocument()
  })

  it('cleans up the previous render', () => {
    expect(screen.queryByText('Rendered')).not.toBeInTheDocument()
  })
})
