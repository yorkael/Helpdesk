import { Link, Outlet } from 'react-router'

export function AppLayout() {
  return (
    <div className="flex min-h-dvh flex-col">
      <a
        href="#main"
        className="fixed top-3 left-3 z-10 rounded-control border border-line-strong bg-surface px-4 py-2 text-sm font-semibold text-ink shadow-paper not-focus:sr-only"
      >
        Skip to content
      </a>
      <header className="border-b border-line bg-surface">
        <div className="mx-auto flex h-14 w-full max-w-5xl items-center px-4 sm:px-6">
          <Link to="/" className="font-display text-lg font-bold tracking-tight text-ink">
            Helpdesk
          </Link>
        </div>
      </header>
      {/* tabIndex -1 lets the skip link move focus here; the main region itself is not a tab stop. */}
      <main id="main" tabIndex={-1} className="mx-auto w-full max-w-5xl flex-1 px-4 py-8 focus:outline-none sm:px-6">
        <Outlet />
      </main>
    </div>
  )
}
