import { useQuery } from '@tanstack/react-query'
import { act, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { createFakeSession } from '../../test/fakeSession'
import { renderWithSession } from '../../test/renderWithSession'

function CachedData() {
  const { data } = useQuery({ queryKey: ['tickets'], queryFn: async () => 'Cached tickets' })
  return <p>{data ?? 'Nothing cached'}</p>
}

describe('SessionProvider', () => {
  it('drops every cached query when the session ends', async () => {
    const fake = createFakeSession('authenticated')
    const { queryClient } = renderWithSession(<CachedData />, fake.session)
    expect(await screen.findByText('Cached tickets')).toBeInTheDocument()

    act(() => fake.end('expired'))

    expect(queryClient.getQueryCache().getAll()).toEqual([])
  })
})
