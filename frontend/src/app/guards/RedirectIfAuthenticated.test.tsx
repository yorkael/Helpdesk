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
  return render(
    <SessionContext value={session}>
      <RouterProvider router={router} />
    </SessionContext>,
  )
}

describe('RedirectIfAuthenticated', () => {
  it('sends a signed-in user of any role to their landing page', async () => {
    const roles: Role[] = ['Client', 'Agent', 'Admin']

    for (const role of roles) {
      const { unmount } = renderAt('/login', createFakeSession('authenticated', userWithRole(role)).session)
      // The role travels with the result, so a failure names the role that broke.
      await vi.waitFor(() => expect({ role, home: screen.queryByText('Home page') !== null }).toEqual({ role, home: true }))
      unmount()
    }
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
