import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react'
import { cx } from '../lib/cx'
import { FieldContext, useFieldControl } from './fieldContext'

type FieldProps = {
  label: string
  hint?: string
  error?: string
  className?: string
  children: ReactNode
}

export function Field({ label, hint, error, className, children }: FieldProps) {
  const controlId = useId()
  const hintId = `${controlId}-hint`
  const errorId = `${controlId}-error`
  const describedBy = cx(hint && hintId, error && errorId) || undefined

  return (
    <div className={cx('flex flex-col gap-1.5', className)}>
      <label htmlFor={controlId} className="text-sm font-semibold text-ink">
        {label}
      </label>
      {hint && (
        <p id={hintId} className="text-sm text-muted">
          {hint}
        </p>
      )}
      <FieldContext.Provider value={{ controlId, describedBy, invalid: Boolean(error) }}>{children}</FieldContext.Provider>
      {error && (
        <p id={errorId} className="text-sm font-medium text-danger">
          {error}
        </p>
      )}
    </div>
  )
}

// The label points at the id from useId. TypeScript does not check hyphenated JSX attributes, so aria-*
// cannot be omitted the same way: the Field's attributes are spread after the consumer's props instead.
type ControlProps<Attributes> = Omit<Attributes, 'id'>

function controlClasses(invalid: boolean, ...extra: Array<string | undefined>): string {
  return cx(
    'w-full rounded-control border bg-surface px-3 text-sm text-ink',
    invalid ? 'border-danger' : 'border-control-line',
    ...extra,
  )
}

export function Input({
  className,
  'aria-describedby': ownDescribedBy,
  ...props
}: ControlProps<InputHTMLAttributes<HTMLInputElement>>) {
  const { invalid, attributes } = useFieldControl(ownDescribedBy)
  return <input {...props} {...attributes} className={controlClasses(invalid, 'h-10', className)} />
}

export function Select({
  className,
  'aria-describedby': ownDescribedBy,
  ...props
}: ControlProps<SelectHTMLAttributes<HTMLSelectElement>>) {
  const { invalid, attributes } = useFieldControl(ownDescribedBy)
  return <select {...props} {...attributes} className={controlClasses(invalid, 'h-10', className)} />
}

export function Textarea({
  className,
  'aria-describedby': ownDescribedBy,
  ...props
}: ControlProps<TextareaHTMLAttributes<HTMLTextAreaElement>>) {
  const { invalid, attributes } = useFieldControl(ownDescribedBy)
  return <textarea {...props} {...attributes} className={controlClasses(invalid, 'min-h-24 py-2', className)} />
}
