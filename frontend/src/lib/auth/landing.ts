import type { Role } from './types'

// Where each role starts after signing in: staff on the ticket list, a client on the home page until the
// client's own screens exist.
const LANDING_PATHS: Record<Role, string> = {
  Client: '/',
  Agent: '/tickets',
  Admin: '/tickets',
}

export function landingPathFor(role: Role): string {
  return LANDING_PATHS[role]
}
