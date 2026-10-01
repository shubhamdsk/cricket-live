import { Link, useParams } from 'react-router-dom'

import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { focusRing } from '@/components/common/focusRing'
import { MatchCard } from '@/components/match/MatchCard'
import { PointsTable } from '@/features/series/components/PointsTable'
import { heldCount } from '@/features/series/held'
import { useSeriesDetails } from '@/features/series/hooks/useSeries'
import { usePageTitle } from '@/hooks/usePageTitle'
import { ApiError } from '@/services/apiClient'
import { cn } from '@/utils/cn'

export function SeriesDetailsPage() {
  const { slug } = useParams<{ slug: string }>()
  const { data, isPending, isError, error, refetch } = useSeriesDetails(slug)

  usePageTitle(data?.series.name ?? null)

  if (isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-20 w-full rounded-xl" />
        <Skeleton className="h-64 w-full rounded-xl" />
      </div>
    )
  }

  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <div className="space-y-4">
          {/*
            A 404 now means something narrower than it used to. The provider's index does not
            list this series and we hold no match of it either, so there is no name to put at the
            top of a page — which is different from a series we know of but hold nothing from.
          */}
          <EmptyState
            title="Series not found"
            description="Neither the provider's series list nor our own records mention this one. It may have finished before either covered it."
          />
          <Link
            to="/series"
            className={cn(
              'rounded-md text-sm font-medium text-brand-strong hover:underline',
              focusRing,
            )}
          >
            Browse series
          </Link>
        </div>
      )
    }

    return (
      <ErrorState
        description={error.message}
        onRetry={() => {
          void refetch()
        }}
      />
    )
  }

  const { series, matches, standings } = data

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <Link
          to="/series"
          className={cn(
            'rounded-md text-sm font-medium text-brand-strong hover:underline',
            focusRing,
          )}
        >
          ← Series
        </Link>

        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">
            {series.name}
          </h1>
          {series.isOngoing && (
            <span className="rounded-full bg-live/10 px-2 py-0.5 text-xs font-medium text-live">
              Ongoing
            </span>
          )}
        </div>

        <p className="text-sm text-ink-subtle">{heldCount(series)}.</p>
      </div>

      {/* Rendered only when a source supplied one. Absence is not an empty table. */}
      <PointsTable standings={standings} />

      <section className="space-y-3">
        <h2 className="text-lg font-semibold tracking-tight text-ink">Matches</h2>

        {matches.length === 0 ? (
          // Reached by a real series the provider's index listed and we hold no match of, which
          // is the normal case for anything outside the live window and our own history. Saying
          // why is the difference between an explanation and a dead end.
          <EmptyState
            title="No matches held for this series"
            description="We list it because the provider does, but we only hold matches from the live window and from our own archive, which starts when this site did."
          />
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {matches.map((match) => (
              <MatchCard key={match.id} match={match} />
            ))}
          </div>
        )}
      </section>
    </div>
  )
}
