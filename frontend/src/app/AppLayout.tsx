import { Link, NavLink, Outlet } from 'react-router'
import { STAFF_ROLES } from '../lib/auth/staff'
import { Button } from '../ui/Button'
import { useSessionManager, useSessionState } from './session/sessionContext'

export function AppLayout() {
  const user = useSessionState((state) => (state.status === 'authenticated' ? state.user : null))
  const manager = useSessionManager()

  return (
    <div className="flex min-h-dvh flex-col">
      <a
        href="#main"
        className="fixed top-3 left-3 z-10 rounded-control border border-line-strong bg-surface px-4 py-2 text-sm font-semibold text-ink shadow-paper not-focus:sr-only"
      >
        Skip to content
      </a>
      <header className="border-b border-line bg-surface">
        <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex items-center gap-6">
            <Link to="/" className="font-display text-lg font-bold tracking-tight text-ink">
              Helpdesk
            </Link>
            {/* A link only for the roles the route lets in; NavLink marks the current page with aria-current. */}
            {user && STAFF_ROLES.includes(user.role) && (
              <nav aria-label="Main">
                <NavLink to="/tickets" className="text-sm font-semibold text-muted aria-[current=page]:text-ink">
                  Tickets
                </NavLink>
              </nav>
            )}
          </div>
          {/* No navigation here: RequireAuth sees the session end and sends the user to the login page. */}
          {user && (
            <div className="flex items-center gap-3">
              <span className="truncate text-sm text-muted">{user.name}</span>
              <Button variant="secondary" onClick={() => void manager.logout()}>
                Sign out
              </Button>
            </div>
          )}
        </div>
      </header>
      {/* tabIndex -1 lets the skip link move focus here; the main region itself is not a tab stop. */}
      <main id="main" tabIndex={-1} className="mx-auto w-full max-w-5xl flex-1 px-4 py-8 focus:outline-none sm:px-6">
        <Outlet />
      </main>
    </div>
  )
}
