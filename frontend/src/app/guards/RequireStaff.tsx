import type { Role } from '../../lib/auth/types'
import { RequireRole } from './RequireRole'

// Pages for the people who work the tickets. UX only, like RequireRole: the API checks every request.
const STAFF: readonly Role[] = ['Agent', 'Admin']

export function RequireStaff() {
  return <RequireRole allow={STAFF} />
}
