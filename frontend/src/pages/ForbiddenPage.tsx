import { Link } from 'react-router'

export function ForbiddenPage() {
  return (
    <div className="flex flex-col items-start gap-3 py-16">
      <h1 className="text-3xl text-ink">You don't have access to this page</h1>
      <p className="text-muted">Your account can't open this page. You are still signed in.</p>
      <Link to="/" className="font-semibold text-primary underline underline-offset-4">
        Back to home
      </Link>
    </div>
  )
}
