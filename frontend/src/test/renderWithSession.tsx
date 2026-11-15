import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import type { AppSession } from '../app/session/createAppSession'
import { SessionProvider } from '../app/session/SessionProvider'

// The providers App puts around the router, with a session the test controls, usually createTab(server).
export function renderWithSession(ui: ReactNode, session: AppSession, queryClient = new QueryClient()) {
  const { unmount } = render(
    <QueryClientProvider client={queryClient}>
      <SessionProvider session={session}>{ui}</SessionProvider>
    </QueryClientProvider>,
  )
  return { queryClient, unmount }
}
