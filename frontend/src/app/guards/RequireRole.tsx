import { Outlet } from 'react-router'
import type { Role } from '../../lib/auth/types'
import { ForbiddenPage } from '../../pages/ForbiddenPage'
import { useSessionState } from '../session/sessionContext'

// UX only: the API answers 403 on its own. A role that may not see the page keeps its session.
export function RequireRole({ allow }: { allow: readonly Role[] }) {
  const role = useSessionState((state) => state.user?.role)

  return role !== undefined && allow.includes(role) ? <Outlet /> : <ForbiddenPage />
}
