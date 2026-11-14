import { vi } from 'vitest'
import type { SessionManager } from '../lib/auth/sessionManager'
import { createSessionStore, type SessionEndReason, type SessionStatus } from '../lib/auth/sessionStore'
import type { CurrentUser, Role } from '../lib/auth/types'

export const userWithRole = (role: Role): CurrentUser => ({
  id: `user-${role.toLowerCase()}`,
  name: `${role} User`,
  email: `${role.toLowerCase()}@example.com`,
  role,
})

// A session for component tests: a real store set by hand and a manager of mocks, so a component's tests never
// depend on how the real manager reaches a state. Only the deliberate integration tests use the real one.
export function createFakeSession(status: SessionStatus = 'restoring', user: CurrentUser | null = null) {
  const store = createSessionStore()
  store.setState({ status, user })
  const listeners = new Set<(reason: SessionEndReason) => void>()
  const manager = {
    accessToken: vi.fn<SessionManager['accessToken']>(() => null),
    refresh: vi.fn<SessionManager['refresh']>(async () => undefined),
    expire: vi.fn<SessionManager['expire']>(async () => undefined),
    restore: vi.fn<SessionManager['restore']>(async () => undefined),
    retryRestore: vi.fn<SessionManager['retryRestore']>(async () => undefined),
    login: vi.fn<SessionManager['login']>(async () => undefined),
    logout: vi.fn<SessionManager['logout']>(async () => undefined),
    onEnded: vi.fn<SessionManager['onEnded']>((listener) => {
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
      }
    }),
  } satisfies SessionManager

  return {
    session: { manager, store },
    manager,
    store,
    /** Ends the session the way the real manager announces it, without touching the store. */
    end(reason: SessionEndReason) {
      for (const listener of listeners) {
        listener(reason)
      }
    },
  }
}
