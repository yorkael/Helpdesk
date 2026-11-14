import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, type RouteObject } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ANA_CREDENTIALS, createFakeAuthServer } from '../test/fakeAuthServer'
import { createFakeSession, userWithRole } from '../test/fakeSession'
import { renderWithSession } from '../test/renderWithSession'
import { createTab } from '../test/sessionTab'
import { SLOW_RENDER_WAIT_MS, SLOW_TEST_TIMEOUT_MS } from '../test/timeouts'
import { routes } from './router'
import type { AppSession } from './session/createAppSession'
import { SessionContext } from './session/sessionContext'
import { SessionGate } from './session/SessionGate'

function renderAt(path: string, session: AppSession) {
  render(
    <SessionContext value={session}>
      <RouterProvider router={createMemoryRouter(routes, { initialEntries: [path] })} />
    </SessionContext>,
  )
}

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
    const router = createMemoryRouter(routes, { initialEntries: ['/tickets/42?x=1#c'] })
    renderWithSession(
      <SessionGate>
        <RouterProvider router={router} />
      </SessionGate>,
      createTab(createFakeAuthServer()),
    )

    await user.type(await screen.findByLabelText('Email', {}, { timeout: SLOW_RENDER_WAIT_MS }), ANA_CREDENTIALS.email)
    await user.type(screen.getByLabelText('Password'), ANA_CREDENTIALS.password)
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument()
    expect(router.state.location).toMatchObject({ pathname: '/tickets/42', search: '?x=1', hash: '#c' })
  })
})
