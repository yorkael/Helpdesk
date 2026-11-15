import { Navigate, Outlet, useLocation } from 'react-router'
import { useSessionState } from '../session/sessionContext'

// UX only, like every guard: the API authorizes each request on its own.
export function RequireAuth() {
  const authenticated = useSessionState((state) => state.status === 'authenticated')
  const signedOut = useSessionState((state) => state.endReason === 'signedOut')
  const location = useLocation()

  if (!authenticated) {
    // The whole address, so signing in brings the user back to exactly what they asked for. Not after a deliberate
    // sign-out: whoever signs in next starts from their own landing page.
    const state = signedOut ? undefined : { from: location.pathname + location.search + location.hash }
    return <Navigate to="/login" replace state={state} />
  }
  return <Outlet />
}
