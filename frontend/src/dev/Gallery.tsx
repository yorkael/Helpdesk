import type { ReactNode } from 'react'
import { TICKET_PRIORITIES, TICKET_STATUSES } from '../lib/ticket'
import { Badge } from '../ui/Badge'
import { Button } from '../ui/Button'
import { EmptyState } from '../ui/EmptyState'
import { Field, Input } from '../ui/Field'
import { PriorityIndicator } from '../ui/PriorityIndicator'
import { LoadingRegion, Skeleton } from '../ui/Skeleton'
import { StatusBadge } from '../ui/StatusBadge'

function GallerySection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-xl text-ink">{title}</h2>
      <div className="flex flex-wrap items-start gap-3">{children}</div>
    </section>
  )
}

// Development only: the router registers this page behind import.meta.env.DEV. Static states, no behavior.
export function Gallery() {
  return (
    <div className="flex flex-col gap-10">
      <h1 className="text-3xl text-ink">Fieldnote component gallery</h1>

      <GallerySection title="Status">
        {TICKET_STATUSES.map((status) => (
          <StatusBadge key={status} status={status} />
        ))}
      </GallerySection>

      <GallerySection title="Priority">
        {TICKET_PRIORITIES.map((priority) => (
          <PriorityIndicator key={priority} priority={priority} />
        ))}
      </GallerySection>

      <GallerySection title="Badge">
        <Badge tone="neutral">Agent</Badge>
        <Badge tone="primary">Admin</Badge>
        <Badge tone="internal">Internal</Badge>
      </GallerySection>

      <GallerySection title="Field">
        <Field label="Email" hint="Use the address you signed up with." className="w-72">
          <Input type="email" placeholder="you@example.com" />
        </Field>
        <Field label="Title" error="Enter a title." className="w-72">
          <Input />
        </Field>
      </GallerySection>

      <GallerySection title="Button">
        <Button>Create ticket</Button>
        <Button variant="secondary">Cancel</Button>
        <Button variant="ghost">Edit</Button>
        <Button loading loadingLabel="Saving…">
          Save
        </Button>
      </GallerySection>

      <GallerySection title="Loading">
        <LoadingRegion label="Loading tickets…" className="flex w-full max-w-md flex-col gap-2">
          <Skeleton className="h-5 w-2/3" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-5/6" />
        </LoadingRegion>
      </GallerySection>

      <GallerySection title="Empty state">
        <EmptyState
          className="w-full"
          headingLevel={3}
          title="No tickets yet"
          description="Tickets you create appear here."
          action={<Button variant="secondary">New ticket</Button>}
        />
      </GallerySection>
    </div>
  )
}
