import { STAFF_ROLES } from '../../lib/auth/staff'
import { RequireRole } from './RequireRole'

// UX only, like RequireRole: the API checks every request.
export function RequireStaff() {
  return <RequireRole allow={STAFF_ROLES} />
}
