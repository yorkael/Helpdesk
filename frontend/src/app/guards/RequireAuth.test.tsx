import { render, screen } from '@testing-library/react'
import { createMemoryRouter, useLocation } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it, vi } from 'vitest'
import { createFakeSession, userWithRole } from '../../test/fakeSession'
import type { AppSession } from '../session/createAppSession'
import { SessionContext } from '../session/sessionContext'
import { RequireAuth } from './RequireAuth'

function SignInPage() {
  const from = (useLocation().state as { from?: string } | null)?.from
  return <p>Sign-in page{from === undefined ? '' : `, back to ${from}`}</p>
}

function renderAt(path: string, session: AppSession) {
  const router = createMemoryRouter(
    [
      { path: '/login', Component: SignInPage },
      { Component: RequireAuth, children: [{ path: '/private', Component: () => <p>Private page</p> }] },
    ],
    { initialEntries: [path] },
  )
  return render(
    <SessionContext value={session}>
      <RouterProvider router={router} />
    </SessionContext>,
  )
}

describe('RequireAuth', () => {
  // One test for one rule: where a signed-out visitor goes, and whether the page is remembered. Only a deliberate
  // sign-out forgets it, so whoever signs in next starts from their own landing page.
  it('sends a signed-out visitor to the sign-in page, remembering the page unless they signed out', async () => {
    const cases = [
      { endReason: null, text: 'Sign-in page, back to /private' },
      { endReason: 'expired', text: 'Sign-in page, back to /private' },
      { endReason: 'endedElsewhere', text: 'Sign-in page, back to /private' },
      { endReason: 'signedOut', text: 'Sign-in page' },
    ] as const

    for (const { endReason, text } of cases) {
      const { unmount } = renderAt('/private', createFakeSession('anonymous', null, endReason).session)

      // The reason travels with the result, so a failure names the case that broke.
      await vi.waitFor(() =>
        expect({ endReason, text: screen.queryByText(/^Sign-in page/)?.textContent ?? null }).toEqual({ endReason, text }),
      )
      expect(screen.queryByText('Private page')).not.toBeInTheDocument()
      unmount()
    }
  })

  it('shows the page to a signed-in user', async () => {
    renderAt('/private', createFakeSession('authenticated', userWithRole('Client')).session)

    expect(await screen.findByText('Private page')).toBeInTheDocument()
  })
})
