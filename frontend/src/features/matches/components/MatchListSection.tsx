import type { UseQueryResult } from '@tanstack/react-query'
import { Link } from 'react-router-dom'

import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { focusRing } from '@/components/common/focusRing'
import { MatchCard } from '@/components/match/MatchCard'
import { MatchCardSkeleton } from '@/components/match/MatchCardSkeleton'
import type { Match } from '@/features/matches/types'
import { cn } from '@/utils/cn'

interface MatchListSectionProps {
  title: string
  query: UseQueryResult<Match[]>
  emptyTitle: string
  emptyDescription?: string
  viewAllTo?: string
  skeletonCount?: number
}

const gridClasses = 'grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3'

/**
 * Cards arrive in sequence rather than together, which reads as a list filling in. The cap matters
 * more than the step: a busy day can return dozens of matches, and without it the last card would
 * wait seconds while the page looks half-loaded.
 */
function staggerDelayMs(index: number): number {
  return Math.min(index, 7) * 40
}

export function MatchListSection({
  title,
  query,
  emptyTitle,
  emptyDescription,
  viewAllTo,
  skeletonCount = 2,
}: MatchListSectionProps) {
  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-lg font-semibold tracking-tight text-ink">{title}</h2>
        {viewAllTo && (
          <Link
            to={viewAllTo}
            className={cn(
              'rounded-md px-1 text-sm font-medium text-brand-strong hover:underline',
              focusRing,
            )}
          >
            View all
          </Link>
        )}
      </div>

      {query.isPending && (
        <div className={gridClasses}>
          {Array.from({ length: skeletonCount }, (_, index) => (
            <MatchCardSkeleton key={index} />
          ))}
        </div>
      )}

      {query.isError && (
        <ErrorState
          description={query.error.message}
          onRetry={() => {
            void query.refetch()
          }}
        />
      )}

      {query.isSuccess &&
        (query.data.length === 0 ? (
          <EmptyState title={emptyTitle} description={emptyDescription} />
        ) : (
          <div className={gridClasses}>
            {query.data.map((match, index) => (
              <div
                key={match.id}
                className="animate-rise"
                style={{ animationDelay: `${staggerDelayMs(index)}ms` }}
              >
                <MatchCard match={match} />
              </div>
            ))}
          </div>
        ))}
    </section>
  )
}
