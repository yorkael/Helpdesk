import { useQuery } from '@tanstack/react-query'
import { act, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { REFRESH_TOKEN_KEY } from '../../lib/auth/refreshTokenStorage'
import { createFakeSession, userWithRole } from '../../test/fakeSession'
import { renderWithSession } from '../../test/renderWithSession'

function CachedData() {
  const { data } = useQuery({ queryKey: ['tickets'], queryFn: async () => 'Cached tickets' })
  return <p>{data ?? 'Nothing cached'}</p>
}

// What the browser delivers to the other tabs when one of them writes to storage.
const storageEvent = (init: StorageEventInit) => new StorageEvent('storage', { storageArea: localStorage, ...init })

function renderSignedIn() {
  const fake = createFakeSession('authenticated', userWithRole('Agent'))
  renderWithSession(<p>Signed-in page</p>, fake.session)
  return fake
}

describe('SessionProvider', () => {
  it('drops every cached query when the session ends', async () => {
    const fake = createFakeSession('authenticated')
    const { queryClient } = renderWithSession(<CachedData />, fake.session)
    expect(await screen.findByText('Cached tickets')).toBeInTheDocument()

    act(() => fake.end('expired'))

    expect(queryClient.getQueryCache().getAll()).toEqual([])
  })

  describe('when another tab writes to storage', () => {
    it('ends the session once the saved token is gone, removed or cleared', () => {
      const cases = [
        { label: 'removed', event: { key: REFRESH_TOKEN_KEY, oldValue: 'refresh-1', newValue: null } },
        { label: 'cleared', event: { key: null, oldValue: null, newValue: null } },
      ]

      for (const { label, event } of cases) {
        const { manager } = renderSignedIn()

        window.dispatchEvent(storageEvent(event))

        // The label travels with the result, so a failure names the case that broke.
        expect({ label, ended: manager.endedInAnotherTab.mock.calls.length }).toEqual({ label, ended: 1 })
      }
    })

    it('keeps the session when the token is only rotated', () => {
      const { manager } = renderSignedIn()

      window.dispatchEvent(storageEvent({ key: REFRESH_TOKEN_KEY, oldValue: 'refresh-1', newValue: 'refresh-2' }))

      expect(manager.endedInAnotherTab).not.toHaveBeenCalled()
    })

    it('keeps the session when another key is removed', () => {
      const { manager } = renderSignedIn()

      window.dispatchEvent(storageEvent({ key: 'some.other.key', oldValue: 'x', newValue: null }))

      expect(manager.endedInAnotherTab).not.toHaveBeenCalled()
    })

    it('keeps the session when the change is in sessionStorage, not localStorage', () => {
      const { manager } = renderSignedIn()

      window.dispatchEvent(storageEvent({ key: REFRESH_TOKEN_KEY, oldValue: 'refresh-1', newValue: null, storageArea: sessionStorage }))

      expect(manager.endedInAnotherTab).not.toHaveBeenCalled()
    })
  })

  it('stops listening to other tabs once unmounted', () => {
    const fake = createFakeSession('authenticated', userWithRole('Agent'))
    const { unmount } = renderWithSession(<p>Signed-in page</p>, fake.session)

    unmount()
    window.dispatchEvent(storageEvent({ key: REFRESH_TOKEN_KEY, oldValue: 'refresh-1', newValue: null }))

    expect(fake.manager.endedInAnotherTab).not.toHaveBeenCalled()
  })
})
