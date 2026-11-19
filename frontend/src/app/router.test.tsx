import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, type RouteObject } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../lib/api/apiClient'
import { ANA_CREDENTIALS, createFakeAuthServer, type FakeAuthServer } from '../test/fakeAuthServer'
import { createFakeSession, userWithRole } from '../test/fakeSession'
import { renderWithSession } from '../test/renderWithSession'
import { REFRESH_TOKEN_KEY } from '../lib/auth/refreshTokenStorage'
import { createRestoredTab, createTab } from '../test/sessionTab'
import { SLOW_RENDER_WAIT_MS, SLOW_TEST_TIMEOUT_MS } from '../test/timeouts'
import { routes } from './router'
import type { AppSession } from './session/createAppSession'
import { SessionContext } from './session/sessionContext'
import { SessionGate } from './session/SessionGate'

// The staff guard is replaced by a marked pass-through: which roles it lets in is RequireStaff.test's job, so a
// change in RequireRole cannot fail these routing tests too. This file only checks what sits behind it.
vi.mock('./guards/RequireStaff', async () => {
  const { Outlet } = await import('react-router')
  return {
    RequireStaff: () => (
      <div data-testid="staff-guard">
        <Outlet />
      </div>
    ),
  }
})

function renderAt(path: string, session: AppSession) {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <SessionContext value={session}>
        <RouterProvider router={createMemoryRouter(routes, { initialEntries: [path] })} />
      </SessionContext>
    </QueryClientProvider>,
  )
}

// The real API client on top of a test tab, so the e2e tests run the whole session the app builds.
const withApiClient = (tab: ReturnType<typeof createTab>, server: FakeAuthServer): AppSession => ({
  ...tab,
  api: createApiClient(server.fetch, tab.manager),
})

const signedIn = () => createFakeSession('authenticated', userWithRole('Agent')).session

function paths(list: RouteObject[]): string[] {
  return list.flatMap((route) => [...(route.path ? [route.path] : []), ...paths(route.children ?? [])])
}

describe('routes', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllEnvs()
    vi.resetModules()
  })

  it('shows the home page at / to a signed-in user', async () => {
    renderAt('/', signedIn())

    expect(await screen.findByRole('heading', { level: 1, name: 'Helpdesk' })).toBeInTheDocument()
  })

  it('shows the not found page for an unknown path to a signed-in user', async () => {
    renderAt('/no-such-page', signedIn())

    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to home' })).toHaveAttribute('href', '/')
  })

  it('puts the ticket list behind the staff guard', async () => {
    const fake = createFakeSession('authenticated', userWithRole('Agent'))
    // The page's filters ask for the categories too.
    fake.request.mockImplementation(async (path) =>
      path === '/api/categories' ? [] : { items: [], page: 1, pageSize: 20, totalCount: 0 },
    )
    renderAt('/tickets', fake.session)

    const guard = await screen.findByTestId('staff-guard')

    expect(within(guard).getByRole('heading', { level: 1, name: 'Tickets' })).toBeInTheDocument()
  })

  it('keeps the paths under /tickets that have no page yet as not found', async () => {
    renderAt('/tickets/42', signedIn())

    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument()
  })

  it('serves the component gallery at /dev/gallery in development, even when signed out', { timeout: SLOW_TEST_TIMEOUT_MS }, async () => {
    renderAt('/dev/gallery', createFakeSession('anonymous').session)

    expect(
      await screen.findByRole(
        'heading',
        { level: 1, name: 'Fieldnote component gallery' },
        { timeout: SLOW_RENDER_WAIT_MS },
      ),
    ).toBeInTheDocument()
  })

  it('does not register the gallery when DEV is false', async () => {
    vi.stubEnv('DEV', false)
    vi.resetModules()
    const { routes: productionRoutes } = await import('./router')

    expect(paths(productionRoutes)).toContain('/')
    expect(paths(productionRoutes)).not.toContain('dev/gallery')
  })

  // Integration on purpose: the real session manager against the fake API, through every guard.
  it('brings a signed-out visitor back to the exact address after signing in', { timeout: SLOW_TEST_TIMEOUT_MS }, async () => {
    const user = userEvent.setup()
    const server = createFakeAuthServer()
    const router = createMemoryRouter(routes, { initialEntries: ['/no-such-page/42?x=1#c'] })
    renderWithSession(
      <SessionGate>
        <RouterProvider router={router} />
      </SessionGate>,
      withApiClient(createTab(server), server),
    )

    await user.type(await screen.findByLabelText('Email', {}, { timeout: SLOW_RENDER_WAIT_MS }), ANA_CREDENTIALS.email)
    await user.type(screen.getByLabelText('Password'), ANA_CREDENTIALS.password)
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument()
    expect(router.state.location).toMatchObject({ pathname: '/no-such-page/42', search: '?x=1', hash: '#c' })
  })

  // Integration on purpose, like the one above: an expiry seen by the real manager, through every guard.
  it('sends a user whose session expired to sign in again and back to the same page', { timeout: SLOW_TEST_TIMEOUT_MS }, async () => {
    const user = userEvent.setup()
    const server = createFakeAuthServer()
    const tab = await createRestoredTab(server)
    const router = createMemoryRouter(routes, { initialEntries: ['/no-such-page/9'] })
    renderWithSession(
      <SessionGate>
        <RouterProvider router={router} />
      </SessionGate>,
      withApiClient(tab, server),
    )
    await screen.findByRole('heading', { level: 1, name: 'Page not found' }, { timeout: SLOW_RENDER_WAIT_MS })

    // What the API client does after a 401: one refresh, which the server now rejects.
    server.expireRefreshToken(localStorage.getItem(REFRESH_TOKEN_KEY)!)
    await act(() => tab.manager.refresh())

    expect(await screen.findByRole('status')).toHaveTextContent('Your session expired. Please sign in again.')
    await user.type(screen.getByLabelText('Email'), ANA_CREDENTIALS.email)
    await user.type(screen.getByLabelText('Password'), ANA_CREDENTIALS.password)
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/no-such-page/9')
  })
})
