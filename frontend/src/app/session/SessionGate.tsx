import { useState, type ReactNode } from 'react'
import { Button } from '../../ui/Button'
import { LoadingRegion, Skeleton } from '../../ui/Skeleton'
import { useSessionManager, useSessionState } from './sessionContext'

// No route renders until the session is known, so a guard never decides on a session that is still loading.
export function SessionGate({ children }: { children: ReactNode }) {
  const status = useSessionState((state) => state.status)
  const manager = useSessionManager()
  const [retrying, setRetrying] = useState(false)

  // No guard against a second click: retryRestore shares the refresh in flight, and the button is loading meanwhile.
  function retry() {
    setRetrying(true)
    void manager.retryRestore().finally(() => setRetrying(false))
  }

  if (status === 'restoring') {
    return (
      <LoadingRegion label="Restoring your session…" className="mx-auto mt-24 flex w-full max-w-sm flex-col gap-3 px-4">
        <Skeleton className="h-6 w-1/2" />
        <Skeleton className="h-4 w-full" />
      </LoadingRegion>
    )
  }

  if (status === 'unreachable') {
    return (
      <main className="mx-auto max-w-md px-4 py-24 text-center">
        <h1 className="text-2xl text-ink">Can't reach the server</h1>
        <p className="mt-2 text-muted">Check your connection and try again.</p>
        <Button className="mt-6" loading={retrying} loadingLabel="Retrying…" onClick={retry}>
          Retry
        </Button>
      </main>
    )
  }

  return children
}
