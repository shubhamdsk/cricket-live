import { Link, useParams } from 'react-router-dom'

import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { focusRing } from '@/components/common/focusRing'
import { MatchCard } from '@/components/match/MatchCard'
import { PointsTable } from '@/features/series/components/PointsTable'
import { useSeriesDetails } from '@/features/series/hooks/useSeries'
import { usePageMeta } from '@/hooks/usePageMeta'
import { ApiError } from '@/services/apiClient'
import { cn } from '@/utils/cn'

export function SeriesDetailsPage() {
  const { slug } = useParams<{ slug: string }>()
  const { data, isPending, isError, error, refetch } = useSeriesDetails(slug)

  usePageMeta(data?.series.name ?? null, {
    description:
      data === undefined
        ? undefined
        : `Full schedule and results for ${data.series.name}: ${data.matches.length} ${
            data.matches.length === 1 ? 'match' : 'matches'
          }, with scores for those already played.`,
  })

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

        {/*
          The list below, counted. Not the "held" figure the card shows: this page merges our own
          records with the provider's fixture list, so what is on screen is usually the whole tour
          and a count of our records would describe something the reader cannot see.
        */}
        <p className="text-sm text-ink-subtle">
          {matches.length} {matches.length === 1 ? 'match' : 'matches'}
          {series.matchCount > 0 && series.matchCount < matches.length
            ? `, ${series.matchCount} with scores recorded here`
            : ''}
          .
        </p>
      </div>

      {/* Rendered only when a source supplied one. Absence is not an empty table. */}
      <PointsTable standings={standings} />

      <section className="space-y-3">
        <h2 className="text-lg font-semibold tracking-tight text-ink">Matches</h2>

        {matches.length === 0 ? (
          // Now a narrow case: the series is real and the provider has no fixtures for it either,
          // which happens for a tour announced before its schedule is published.
          <EmptyState
            title="No matches listed yet"
            description="The series exists, but the provider has not published its fixtures and we hold no record of it. The list fills in once either does."
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
