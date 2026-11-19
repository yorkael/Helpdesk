import { useQueryClient } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { AppProviders } from './providers'

function QueryClientProbe() {
  const queryClient = useQueryClient()
  return <p>{queryClient.isFetching() === 0 ? 'Query client ready' : 'Fetching'}</p>
}

describe('AppProviders', () => {
  it('gives its children a query client', () => {
    render(
      <AppProviders>
        <QueryClientProbe />
      </AppProviders>,
    )

    expect(screen.getByText('Query client ready')).toBeInTheDocument()
  })
})
