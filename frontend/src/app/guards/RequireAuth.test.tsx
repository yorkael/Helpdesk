import { render, screen } from '@testing-library/react'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it } from 'vitest'
import { createFakeSession, userWithRole } from '../../test/fakeSession'
import type { AppSession } from '../session/createAppSession'
import { SessionContext } from '../session/sessionContext'
import { RequireAuth } from './RequireAuth'

function renderAt(path: string, session: AppSession) {
  const router = createMemoryRouter(
    [
      { path: '/login', Component: () => <p>Sign-in page</p> },
      { Component: RequireAuth, children: [{ path: '/private', Component: () => <p>Private page</p> }] },
    ],
    { initialEntries: [path] },
  )
  render(
    <SessionContext value={session}>
      <RouterProvider router={router} />
    </SessionContext>,
  )
}

describe('RequireAuth', () => {
  it('sends a signed-out visitor to the sign-in page', async () => {
    renderAt('/private', createFakeSession('anonymous').session)

    expect(await screen.findByText('Sign-in page')).toBeInTheDocument()
    expect(screen.queryByText('Private page')).not.toBeInTheDocument()
  })

  it('shows the page to a signed-in user', async () => {
    renderAt('/private', createFakeSession('authenticated', userWithRole('Client')).session)

    expect(await screen.findByText('Private page')).toBeInTheDocument()
  })
})
