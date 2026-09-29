import type {
  InfiniteData,
  UseInfiniteQueryResult,
  UseQueryResult,
} from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { focusRing } from '@/components/common/focusRing'
import { MatchCard } from '@/components/match/MatchCard'
import { MatchCardSkeleton } from '@/components/match/MatchCardSkeleton'
import type { Match, Paged } from '@/features/matches/types'
import { cn } from '@/utils/cn'

/**
 * Results are paged and the other lists are not, so the section accepts either. Flattening here
 * rather than at each call site keeps the four pages that render lists identical to read.
 */
type MatchListQuery =
  UseQueryResult<Match[], Error> | UseInfiniteQueryResult<InfiniteData<Paged<Match>>, Error>

interface MatchListSectionProps {
  title: string
  query: MatchListQuery
  emptyTitle: string
  emptyDescription?: string
  viewAllTo?: string
  skeletonCount?: number
  /** Rendered below the cards, for a section that can load more of itself. */
  footer?: ReactNode
}

function matchesOf(data: MatchListQuery['data']): Match[] | undefined {
  if (data === undefined) {
    return undefined
  }

  return Array.isArray(data) ? data : data.pages.flatMap((page) => page.items)
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
  footer,
}: MatchListSectionProps) {
  const matches = matchesOf(query.data)

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

      {matches !== undefined &&
        (matches.length === 0 ? (
          <EmptyState title={emptyTitle} description={emptyDescription} />
        ) : (
          <>
            <div className={gridClasses}>
              {matches.map((match, index) => (
                <div
                  key={match.id}
                  className="animate-rise"
                  style={{ animationDelay: `${staggerDelayMs(index)}ms` }}
                >
                  <MatchCard match={match} />
                </div>
              ))}
            </div>
            {footer}
          </>
        ))}
    </section>
  )
}
