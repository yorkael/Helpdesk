import { useQueryClient } from '@tanstack/react-query'
import { useEffect, type ReactNode } from 'react'
import { REFRESH_TOKEN_KEY } from '../../lib/auth/refreshTokenStorage'
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

  useEffect(() => {
    // The browser fires this only in the other tabs. A rotation writes a new value and is ignored; only a removal
    // of the token, or of everything (key null after clear()), means the session is over.
    function handleStorage(event: StorageEvent) {
      const tokenGone = event.key === REFRESH_TOKEN_KEY || event.key === null
      if (event.storageArea === window.localStorage && tokenGone && event.newValue === null) {
        session.manager.endedInAnotherTab()
      }
    }
    window.addEventListener('storage', handleStorage)
    return () => window.removeEventListener('storage', handleStorage)
  }, [session])

  return <SessionContext value={session}>{children}</SessionContext>
}
