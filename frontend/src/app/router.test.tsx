import { render, screen } from '@testing-library/react'
import { createMemoryRouter, type RouteObject } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { routes } from './router'

function renderAt(path: string) {
  render(<RouterProvider router={createMemoryRouter(routes, { initialEntries: [path] })} />)
}

function paths(list: RouteObject[]): string[] {
  return list.flatMap((route) => [...(route.path ? [route.path] : []), ...paths(route.children ?? [])])
}

describe('routes', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    vi.resetModules()
  })

  it('shows the home page at /', async () => {
    renderAt('/')

    expect(await screen.findByRole('heading', { level: 1, name: 'Helpdesk' })).toBeInTheDocument()
  })

  it('shows the not found page for an unknown path', async () => {
    renderAt('/no-such-page')

    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to home' })).toHaveAttribute('href', '/')
  })

  it('serves the component gallery at /dev/gallery in development', async () => {
    renderAt('/dev/gallery')

    expect(await screen.findByRole('heading', { level: 1, name: 'Fieldnote component gallery' })).toBeInTheDocument()
  })

  it('does not register the gallery when DEV is false', async () => {
    vi.stubEnv('DEV', false)
    vi.resetModules()
    const { routes: productionRoutes } = await import('./router')

    expect(paths(productionRoutes)).toContain('/')
    expect(paths(productionRoutes)).not.toContain('dev/gallery')
  })
})
