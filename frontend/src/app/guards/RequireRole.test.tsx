import { render, screen } from '@testing-library/react'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it } from 'vitest'
import type { Role } from '../../lib/auth/types'
import { createFakeSession, userWithRole } from '../../test/fakeSession'
import { SessionContext } from '../session/sessionContext'
import { RequireRole } from './RequireRole'

// No production route needs a role yet, so a staff-only route exists only here.
function renderStaffPageAs(role: Role) {
  const fake = createFakeSession('authenticated', userWithRole(role))
  const router = createMemoryRouter(
    [{ element: <RequireRole allow={['Agent', 'Admin']} />, children: [{ path: '/staff', Component: () => <p>Staff page</p> }] }],
    { initialEntries: ['/staff'] },
  )
  render(
    <SessionContext value={fake.session}>
      <RouterProvider router={router} />
    </SessionContext>,
  )
  return fake
}

describe('RequireRole', () => {
  it('shows the forbidden page to a role that is not allowed, and keeps the session', async () => {
    const fake = renderStaffPageAs('Client')

    expect(await screen.findByRole('heading', { level: 1, name: "You don't have access to this page" })).toBeInTheDocument()
    expect(screen.queryByText('Staff page')).not.toBeInTheDocument()
    expect(fake.manager.logout).not.toHaveBeenCalled()
    expect(fake.manager.expire).not.toHaveBeenCalled()
  })

  it('shows the page to an allowed role', async () => {
    renderStaffPageAs('Agent')

    expect(await screen.findByText('Staff page')).toBeInTheDocument()
  })
})
