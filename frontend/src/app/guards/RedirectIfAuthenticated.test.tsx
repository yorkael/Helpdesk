import { render, screen } from '@testing-library/react'
import { createMemoryRouter, type InitialEntry } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it, vi } from 'vitest'
import type { Role } from '../../lib/auth/types'
import { createFakeSession, userWithRole } from '../../test/fakeSession'
import type { AppSession } from '../session/createAppSession'
import { SessionContext } from '../session/sessionContext'
import { RedirectIfAuthenticated } from './RedirectIfAuthenticated'

// A stand-in at the layer boundary: which paths are safe is returnTo.test's job, not this guard's.
vi.mock('../../lib/auth/returnTo', () => ({
  safeReturnTo: vi.fn<(value: unknown) => string | null>((value) => (typeof value === 'string' ? value : null)),
}))

function renderAt(entry: InitialEntry, session: AppSession) {
  const router = createMemoryRouter(
    [
      { Component: RedirectIfAuthenticated, children: [{ path: '/login', Component: () => <p>Sign-in page</p> }] },
      { path: '/', Component: () => <p>Home page</p> },
      { path: '/x', Component: () => <p>Page x</p> },
    ],
    { initialEntries: [entry] },
  )
  const { unmount } = render(
    <SessionContext value={session}>
      <RouterProvider router={router} />
    </SessionContext>,
  )
  return { router, unmount }
}

describe('RedirectIfAuthenticated', () => {
  // One assertion on the whole list, so a broken rule shows every role it sends to the wrong place at once.
  it('sends a signed-in user of each role to their landing page', async () => {
    const roles: Role[] = ['Client', 'Agent', 'Admin']
    const destinations: { role: Role; destination: string }[] = []

    for (const role of roles) {
      const { router, unmount } = renderAt('/login', createFakeSession('authenticated', userWithRole(role)).session)
      await vi.waitFor(() => {
        if (router.state.location.pathname === '/login') throw new Error(`${role} is still on /login`)
      })
      destinations.push({ role, destination: router.state.location.pathname })
      unmount()
    }

    expect(destinations).toEqual([
      { role: 'Client', destination: '/' },
      { role: 'Agent', destination: '/tickets' },
      { role: 'Admin', destination: '/tickets' },
    ])
  })

  it('returns a signed-in user to the page they asked for', async () => {
    renderAt({ pathname: '/login', state: { from: '/x' } }, createFakeSession('authenticated', userWithRole('Agent')).session)

    expect(await screen.findByText('Page x')).toBeInTheDocument()
  })

  it('shows the sign-in page to a signed-out visitor', async () => {
    renderAt('/login', createFakeSession('anonymous').session)

    expect(await screen.findByText('Sign-in page')).toBeInTheDocument()
  })
})
