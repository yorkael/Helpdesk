import type { RouteObject } from 'react-router'
import { HomePage } from '../pages/HomePage'
import { NotFoundPage } from '../pages/NotFoundPage'
import { AppLayout } from './AppLayout'

// Keep the condition inline at module level: the production build replaces it with false and drops the
// dynamic import with it, so the gallery never reaches dist/.
const devRoutes: RouteObject[] = import.meta.env.DEV
  ? [{ path: 'dev/gallery', lazy: async () => ({ Component: (await import('../dev/Gallery')).Gallery }) }]
  : []

export const routes: RouteObject[] = [
  {
    path: '/',
    Component: AppLayout,
    children: [{ index: true, Component: HomePage }, ...devRoutes, { path: '*', Component: NotFoundPage }],
  },
]
