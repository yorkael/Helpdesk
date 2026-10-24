import { createContext, useContext } from 'react'
import { cx } from '../lib/cx'

type FieldContextValue = {
  controlId: string
  describedBy: string | undefined
  invalid: boolean
}

export const FieldContext = createContext<FieldContextValue | null>(null)

// The Field's own descriptions come first, then any the consumer adds.
export function useFieldControl(ownDescribedBy: string | undefined) {
  const field = useContext(FieldContext)
  if (!field) throw new Error('Input, Select and Textarea must be rendered inside a Field')
  return {
    invalid: field.invalid,
    attributes: {
      id: field.controlId,
      'aria-describedby': cx(field.describedBy, ownDescribedBy) || undefined,
      'aria-invalid': field.invalid || undefined,
    },
  }
}
