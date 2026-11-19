import { Navigate, Outlet, useLocation } from 'react-router'
import { landingPathFor } from '../../lib/auth/landing'
import { safeReturnTo } from '../../lib/auth/returnTo'
import { useSessionState } from '../session/sessionContext'

function readFrom(state: unknown): unknown {
  return typeof state === 'object' && state !== null && 'from' in state ? state.from : undefined
}

// The only place that decides where a signed-in user goes from the login page, right after a login included.
export function RedirectIfAuthenticated() {
  const user = useSessionState((state) => (state.status === 'authenticated' ? state.user : null))
  const location = useLocation()

  if (user) {
    const destination = safeReturnTo(readFrom(location.state), window.location.origin) ?? landingPathFor(user.role)
    return <Navigate to={destination} replace />
  }
  return <Outlet />
}
