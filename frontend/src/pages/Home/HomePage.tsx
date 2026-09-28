import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { FeaturedMatch } from '@/components/match/FeaturedMatch'
import { MatchListSection } from '@/features/matches/components/MatchListSection'
import {
  useLiveMatches,
  useRecentMatches,
  useUpcomingMatches,
} from '@/features/matches/hooks/useMatches'
import { mockPopularSeries } from '@/features/series/mocks/series'

export function HomePage() {
  const liveQuery = useLiveMatches()
  const upcomingQuery = useUpcomingMatches()
  const recentQuery = useRecentMatches()

  const featuredMatch = liveQuery.data?.[0]

  return (
    <div className="space-y-8">
      <section className="space-y-3">
        <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">
          Featured match
        </h1>

        {liveQuery.isPending && <Skeleton className="h-56 w-full rounded-2xl" />}

        {liveQuery.isError && (
          <ErrorState
            description={liveQuery.error.message}
            onRetry={() => {
              void liveQuery.refetch()
            }}
          />
        )}

        {liveQuery.isSuccess &&
          (featuredMatch ? (
            <FeaturedMatch match={featuredMatch} />
          ) : (
            <EmptyState
              title="No match is live right now"
              description="Upcoming fixtures are listed below."
            />
          ))}
      </section>

      <MatchListSection
        title="Live matches"
        query={liveQuery}
        emptyTitle="No live matches"
        emptyDescription="Check back when the next match gets under way."
        viewAllTo="/live"
      />

      <MatchListSection
        title="Upcoming matches"
        query={upcomingQuery}
        emptyTitle="No upcoming matches"
        emptyDescription="Fixtures will appear here once they are scheduled."
        viewAllTo="/matches"
      />

      <MatchListSection
        title="Recent results"
        query={recentQuery}
        emptyTitle="No recent results"
        emptyDescription="Completed matches will appear here."
        viewAllTo="/matches"
      />

      <section className="space-y-3">
        <h2 className="text-lg font-semibold tracking-tight text-ink">Popular series</h2>
        <ul className="flex flex-wrap gap-2">
          {mockPopularSeries.map((series) => (
            <li
              key={series.id}
              className="rounded-full border border-line bg-surface px-3 py-1.5 text-sm text-ink-muted"
            >
              {series.name}
            </li>
          ))}
        </ul>
        <p className="text-sm text-ink-subtle">Series pages arrive in Sprint 7.</p>
      </section>
    </div>
  )
}
