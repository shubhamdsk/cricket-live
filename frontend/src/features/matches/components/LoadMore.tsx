import { Loader2 } from 'lucide-react'

import { Button } from '@/components/common/Button'

interface LoadMoreProps {
  hasMore: boolean
  isLoading: boolean
  total: number
  shown: number
  onLoadMore: () => void
}

/**
 * A button rather than infinite scroll on scroll position. Results are something people search
 * through, and a list that keeps growing under the scrollbar makes the footer unreachable and
 * takes the back button with it.
 */
export function LoadMore({ hasMore, isLoading, total, shown, onLoadMore }: LoadMoreProps) {
  return (
    <div className="flex flex-col items-center gap-2 pt-2">
      {hasMore && (
        <Button variant="secondary" onClick={onLoadMore} disabled={isLoading}>
          {/* The label already says so, so the icon is decoration and announces nothing. */}
          {isLoading && <Loader2 className="size-4 animate-spin" aria-hidden />}
          {isLoading ? 'Loading' : 'Load more'}
        </Button>
      )}
      {/*
        The count is worth showing because the archive only holds matches played since it started
        keeping them, so a short list is expected rather than a sign that something is missing.
      */}
      <p className="text-xs text-ink-subtle" aria-live="polite">
        Showing {shown} of {total} completed {total === 1 ? 'match' : 'matches'}
      </p>
    </div>
  )
}
