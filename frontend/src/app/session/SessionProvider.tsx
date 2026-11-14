import { useQueryClient } from '@tanstack/react-query'
import { useEffect, type ReactNode } from 'react'
import type { AppSession } from './createAppSession'
import { SessionContext } from './sessionContext'

export function SessionProvider({ session, children }: { session: AppSession; children: ReactNode }) {
  const queryClient = useQueryClient()

  useEffect(() => {
    // Subscribed before restoring, so a session that ends during the restore also drops the previous user's data.
    const unsubscribe = session.manager.onEnded(() => queryClient.clear())
    // StrictMode mounts this effect twice; restore() runs once per manager, so the token is presented once.
    void session.manager.restore()
    return unsubscribe
  }, [session, queryClient])

  return <SessionContext value={session}>{children}</SessionContext>
}
