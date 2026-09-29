import { Link, useParams } from 'react-router-dom'

import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { focusRing } from '@/components/common/focusRing'
import { MatchCard } from '@/components/match/MatchCard'
import { TeamCrest } from '@/features/teams/components/TeamCrest'
import { useTeamDetails } from '@/features/teams/hooks/useTeams'
import { ApiError } from '@/services/apiClient'
import { cn } from '@/utils/cn'

export function TeamDetailsPage() {
  const { slug } = useParams<{ slug: string }>()
  const { data, isPending, isError, error, refetch } = useTeamDetails(slug)

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
          <EmptyState
            title="Team not found"
            description="We hold no matches for this side, so there is nothing to show. It may not have played since this site started recording."
          />
          <Link
            to="/teams"
            className={cn(
              'rounded-md text-sm font-medium text-brand-strong hover:underline',
              focusRing,
            )}
          >
            All teams
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

  const { team, matches, series, opponents, formats } = data

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <Link
          to="/teams"
          className={cn(
            'rounded-md text-sm font-medium text-brand-strong hover:underline',
            focusRing,
          )}
        >
          ← All teams
        </Link>

        <div className="flex items-center gap-3">
          <TeamCrest team={team} className="size-12" />

          <div>
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">
                {team.name}
              </h1>
              {team.isActive && (
                <span className="rounded-full bg-live/10 px-2 py-0.5 text-xs font-medium text-live">
                  Playing
                </span>
              )}
            </div>

            <p className="text-sm text-ink-subtle">
              {team.matchCount} {team.matchCount === 1 ? 'match' : 'matches'} held here
              {formats.length > 0 && (
                <>
                  {' '}
                  · {formats.map((entry) => `${entry.matchCount} ${entry.format}`).join(', ')}
                </>
              )}
            </p>
          </div>
        </div>
      </div>

      {/*
        No won-lost record, and its absence is the point. The provider reports a result only as a
        sentence, so a record would have to be parsed out of prose and shown as fact.
      */}

      {series.length > 0 && (
        <section className="space-y-3">
          <h2 className="text-lg font-semibold tracking-tight text-ink">Series</h2>
          <ul className="flex flex-wrap gap-2">
            {series.map((entry) => (
              <li key={entry.id}>
                <Link
                  to={`/series/${entry.slug}`}
                  className={cn(
                    'inline-flex items-center gap-2 rounded-full border border-line bg-surface px-3 py-1.5 text-sm text-ink transition hover:border-brand/40',
                    focusRing,
                  )}
                >
                  {entry.name}
                  <span className="text-xs text-ink-subtle">{entry.matchCount}</span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      {opponents.length > 0 && (
        <section className="space-y-3">
          <h2 className="text-lg font-semibold tracking-tight text-ink">Opponents</h2>
          {/* Counts of meetings, not a head-to-head. Who won is not a fact we hold. */}
          <ul className="flex flex-wrap gap-2">
            {opponents.map((opponent) => (
              <li key={opponent.id}>
                <Link
                  to={`/teams/${opponent.id}`}
                  className={cn(
                    'inline-flex items-center gap-2 rounded-full border border-line bg-surface px-3 py-1.5 text-sm text-ink transition hover:border-brand/40',
                    focusRing,
                  )}
                >
                  {opponent.name}
                  <span className="text-xs text-ink-subtle">
                    {opponent.matchCount === 1
                      ? '1 meeting'
                      : `${opponent.matchCount} meetings`}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="space-y-3">
        <h2 className="text-lg font-semibold tracking-tight text-ink">Matches</h2>

        {matches.length === 0 ? (
          <EmptyState title="No matches recorded" />
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
