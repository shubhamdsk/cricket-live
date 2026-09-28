import { AlertTriangle } from 'lucide-react'

import { Button } from '@/components/common/Button'

interface ErrorStateProps {
  title?: string
  description?: string
  onRetry?: () => void
}

export function ErrorState({
  title = 'Something went wrong',
  description = 'We could not load this information. Please try again.',
  onRetry,
}: ErrorStateProps) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center gap-3 rounded-card border border-danger-line bg-danger-soft px-6 py-10 text-center"
    >
      <AlertTriangle className="size-6 text-danger" aria-hidden />
      <div className="space-y-1">
        <p className="font-medium text-danger">{title}</p>
        <p className="max-w-sm text-sm text-ink-muted">{description}</p>
      </div>
      {onRetry && (
        <Button variant="secondary" size="sm" onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  )
}
