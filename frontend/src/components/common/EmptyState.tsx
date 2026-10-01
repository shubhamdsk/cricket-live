import { Inbox } from 'lucide-react'

interface EmptyStateProps {
  title: string
  description?: string
}

export function EmptyState({ title, description }: EmptyStateProps) {
  return (
    <div className="glass flex flex-col items-center gap-2 rounded-card border border-dashed border-line-strong bg-surface px-6 py-10 text-center">
      <Inbox className="size-6 text-ink-subtle" aria-hidden />
      <p className="font-medium text-ink">{title}</p>
      {description && <p className="max-w-sm text-sm text-ink-muted">{description}</p>}
    </div>
  )
}
