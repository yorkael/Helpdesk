import type { RouteObject } from 'react-router'
import { HomePage } from '../pages/HomePage'
import { LoginPage } from '../pages/LoginPage'
import { NotFoundPage } from '../pages/NotFoundPage'
import { AppLayout } from './AppLayout'
import { RedirectIfAuthenticated } from './guards/RedirectIfAuthenticated'
import { RequireAuth } from './guards/RequireAuth'

// Keep the condition inline at module level: the production build replaces it with false and drops the
// dynamic import with it, so the gallery never reaches dist/.
const devRoutes: RouteObject[] = import.meta.env.DEV
  ? [{ path: 'dev/gallery', lazy: async () => ({ Component: (await import('../dev/Gallery')).Gallery }) }]
  : []

export const routes: RouteObject[] = [
  {
    path: '/',
    Component: AppLayout,
    children: [
      { Component: RedirectIfAuthenticated, children: [{ path: 'login', Component: LoginPage }] },
      ...devRoutes,
      // Unknown paths too: a signed-out visitor sees the login page, not which pages exist.
      { Component: RequireAuth, children: [{ index: true, Component: HomePage }, { path: '*', Component: NotFoundPage }] },
    ],
  },
]
