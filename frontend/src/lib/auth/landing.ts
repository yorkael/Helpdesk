import type { Role } from './types'

// Every role starts on the home page until the ticket screens exist; this is the one place to change then.
const LANDING_PATHS: Record<Role, string> = {
  Client: '/',
  Agent: '/',
  Admin: '/',
}

export function landingPathFor(role: Role): string {
  return LANDING_PATHS[role]
}
