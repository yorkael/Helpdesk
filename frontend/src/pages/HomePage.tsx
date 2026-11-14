import { useSessionState } from '../app/session/sessionContext'

export function HomePage() {
  const user = useSessionState((state) => state.user)

  return (
    <div className="py-16 text-center">
      <h1 className="text-4xl text-ink">Helpdesk</h1>
      <p className="mt-2 text-muted">Support ticket system</p>
      {user && (
        <p className="mt-6 text-ink">
          Signed in as {user.name} ({user.role})
        </p>
      )}
    </div>
  )
}
