import { useState } from 'react'
import { createBrowserRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { AppProviders } from './providers'
import { routes } from './router'
import { createAppSession } from './session/createAppSession'
import { SessionGate } from './session/SessionGate'
import { SessionProvider } from './session/SessionProvider'

const router = createBrowserRouter(routes)

export function App() {
  // One session for the app's lifetime: a new one on each render would present the refresh token again.
  const [session] = useState(createAppSession)

  return (
    <AppProviders>
      <SessionProvider session={session}>
        <SessionGate>
          <RouterProvider router={router} />
        </SessionGate>
      </SessionProvider>
    </AppProviders>
  )
}
