import type { Role } from './types'

/** The people who work the tickets. The one place that says so; RequireStaff and the header read it. */
export const STAFF_ROLES: readonly Role[] = ['Agent', 'Admin']
