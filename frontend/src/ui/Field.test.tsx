import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Field, Input, Select, Textarea } from './Field'

const CONTROLS: Array<[name: string, control: () => ReactNode, tagName: string]> = [
  ['Input', () => <Input />, 'INPUT'],
  [
    'Select',
    () => (
      <Select>
        <option>Billing</option>
      </Select>
    ),
    'SELECT',
  ],
  ['Textarea', () => <Textarea />, 'TEXTAREA'],
]

afterEach(() => {
  vi.restoreAllMocks()
})

describe('Field', () => {
  it.each(CONTROLS)('labels its %s', (_name, control, tagName) => {
    render(<Field label="Category">{control()}</Field>)

    expect(screen.getByLabelText('Category').tagName).toBe(tagName)
  })

  it('leaves the control without description or invalid state when there is no hint or error', () => {
    render(
      <Field label="Title">
        <Input />
      </Field>,
    )
    const input = screen.getByLabelText('Title')

    expect(input).not.toHaveAttribute('aria-describedby')
    expect(input).not.toHaveAttribute('aria-invalid')
  })

  it('gives each field its own id', () => {
    render(
      <>
        <Field label="Title">
          <Input />
        </Field>
        <Field label="Description">
          <Textarea />
        </Field>
      </>,
    )

    expect(screen.getByLabelText('Title').id).not.toBe(screen.getByLabelText('Description').id)
  })

  it('throws when a control is rendered outside a Field', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<Input />)).toThrow('Input, Select and Textarea must be rendered inside a Field')
  })

  it.each(CONTROLS)('describes its %s with the error', (_name, control) => {
    render(
      <Field label="Title" error="Enter a title.">
        {control()}
      </Field>,
    )

    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription('Enter a title.')
  })

  it.each(CONTROLS)('marks its %s invalid when there is an error', (_name, control) => {
    render(
      <Field label="Title" error="Enter a title.">
        {control()}
      </Field>,
    )

    expect(screen.getByLabelText('Title')).toHaveAttribute('aria-invalid', 'true')
  })

  it.each(CONTROLS)('describes its %s with the hint', (_name, control) => {
    render(
      <Field label="Title" hint="A short summary of the problem.">
        {control()}
      </Field>,
    )

    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription('A short summary of the problem.')
  })

  it.each(CONTROLS)('describes its %s with the hint and the error together', (_name, control) => {
    render(
      <Field label="Title" hint="A short summary of the problem." error="Enter a title.">
        {control()}
      </Field>,
    )

    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription('A short summary of the problem. Enter a title.')
  })

  it.each([
    ['Input', (describedBy: string) => <Input aria-describedby={describedBy} />],
    [
      'Select',
      (describedBy: string) => (
        <Select aria-describedby={describedBy}>
          <option>Billing</option>
        </Select>
      ),
    ],
    ['Textarea', (describedBy: string) => <Textarea aria-describedby={describedBy} />],
  ] as const)("adds the consumer's aria-describedby after the Field's own on its %s", (_name, control) => {
    const { rerender } = render(
      <>
        <p id="customer-note">Visible to the customer.</p>
        <Field label="Title" hint="A short summary of the problem.">
          {control('customer-note')}
        </Field>
      </>,
    )

    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription('A short summary of the problem. Visible to the customer.')

    rerender(
      <>
        <p id="customer-note">Visible to the customer.</p>
        <Field label="Title">{control('customer-note')}</Field>
      </>,
    )

    expect(screen.getByLabelText('Title')).toHaveAccessibleDescription('Visible to the customer.')
  })

  it.each([
    ['Input', (invalid: 'true' | 'false') => <Input aria-invalid={invalid} />],
    [
      'Select',
      (invalid: 'true' | 'false') => (
        <Select aria-invalid={invalid}>
          <option>Billing</option>
        </Select>
      ),
    ],
    ['Textarea', (invalid: 'true' | 'false') => <Textarea aria-invalid={invalid} />],
  ] as const)('overrides the aria-invalid a consumer passes to its %s', (_name, control) => {
    const { rerender } = render(
      <Field label="Title" error="Enter a title.">
        {control('false')}
      </Field>,
    )

    expect(screen.getByLabelText('Title')).toHaveAttribute('aria-invalid', 'true')

    rerender(<Field label="Title">{control('true')}</Field>)

    expect(screen.getByLabelText('Title')).not.toHaveAttribute('aria-invalid')
  })

  it('does not take an id from the consumer, so the label stays linked to the control', () => {
    render(
      <Field label="Title">
        <Input
          // @ts-expect-error id comes from the Field: its label points at the id from useId
          id="custom-id"
        />
      </Field>,
    )

    expect(screen.getByLabelText('Title').id).not.toBe('custom-id')
  })
})
