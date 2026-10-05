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
import type { DatedMatches, Match, Paged } from '@/features/matches/types'
import { formatAsOf } from '@/features/matches/utils/format'
import { cn } from '@/utils/cn'

/**
 * Three shapes, because the three lists genuinely differ. Results are paged; live and upcoming
 * carry a capture time for when the API had to answer from a stored window; series and team
 * fixtures are a plain array. Unwrapping all three here rather than at each call site keeps the
 * pages that render lists identical to read.
 */
type MatchListQuery =
  | UseQueryResult<Match[], Error>
  | UseQueryResult<DatedMatches, Error>
  | UseInfiniteQueryResult<InfiniteData<Paged<Match>>, Error>

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
  if (data == null) {
    return undefined
  }

  if (Array.isArray(data)) {
    return data
  }

  return 'matches' in data ? data.matches : data.pages.flatMap((page) => page.items)
}

function asOfOf(data: MatchListQuery['data']): string | undefined {
  return data != null && !Array.isArray(data) && 'asOfUtc' in data ? data.asOfUtc : undefined
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
  const asOf = asOfOf(query.data)

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

      {/*
        Above the cards, not below them, because it changes how everything under it should be read.
        Stated as a fact about our records rather than as a warning: the same score unlabelled would
        be a lie told to someone watching a match, and a red banner over a correct fixture list
        would be alarm about nothing. `role="status"` so a screen reader is told once, politely,
        rather than having it announced as an error.
      */}
      {asOf !== undefined && (
        <p
          role="status"
          className="rounded-md border border-warn-line bg-warn-soft px-3 py-2 text-sm text-ink-muted"
        >
          Scores last updated {formatAsOf(asOf)}. The score provider is unavailable, so this is
          the most recent data we have.
        </p>
      )}

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
