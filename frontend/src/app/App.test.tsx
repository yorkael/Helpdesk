import { render, screen } from '@testing-library/react'
import { StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { REFRESH_TOKEN_KEY } from '../lib/auth/refreshTokenStorage'
import { createFakeAuthServer } from '../test/fakeAuthServer'
import { SLOW_RENDER_WAIT_MS, SLOW_TEST_TIMEOUT_MS } from '../test/timeouts'
import { App } from './App'

describe('App', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('restores a saved session once, under StrictMode and across re-renders', { timeout: SLOW_TEST_TIMEOUT_MS }, async () => {
    const server = createFakeAuthServer()
    localStorage.setItem(REFRESH_TOKEN_KEY, server.signIn().refreshToken)
    vi.stubGlobal('fetch', server.fetch)
    // A new element each time: React skips rendering an element it has already rendered.
    const app = () => (
      <StrictMode>
        <App />
      </StrictMode>
    )

    const { rerender } = render(app())
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Helpdesk' }, { timeout: SLOW_RENDER_WAIT_MS }),
    ).toBeInTheDocument()
    await vi.waitFor(() => expect(server.counters.meCalls).toBe(1))
    rerender(app())

    // A session created on that render would start restoring again and hide the page.
    expect(screen.getByRole('heading', { level: 1, name: 'Helpdesk' })).toBeInTheDocument()
    expect(server.counters.refreshCalls).toBe(1)
    expect(server.counters.massRevocations).toBe(0)
  })
})
