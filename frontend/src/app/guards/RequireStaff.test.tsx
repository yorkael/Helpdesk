import { render } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { RequireRole } from './RequireRole'
import { RequireStaff } from './RequireStaff'

// RequireRole has its own tests; here it is replaced so a change in it cannot fail this one too.
vi.mock('./RequireRole', () => ({ RequireRole: vi.fn<typeof RequireRole>(() => <p>Guarded</p>) }))

describe('RequireStaff', () => {
  it('allows only agents and admins', () => {
    render(<RequireStaff />)

    expect(vi.mocked(RequireRole).mock.calls.map(([props]) => props)).toEqual([{ allow: ['Agent', 'Admin'] }])
  })
})
