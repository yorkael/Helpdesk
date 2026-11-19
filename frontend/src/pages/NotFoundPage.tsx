import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <div className="flex flex-col items-start gap-3 py-16">
      <h1 className="text-3xl text-ink">Page not found</h1>
      <p className="text-muted">The page you are looking for does not exist or has moved.</p>
      <Link to="/" className="font-semibold text-primary underline underline-offset-4">
        Back to home
      </Link>
    </div>
  )
}
