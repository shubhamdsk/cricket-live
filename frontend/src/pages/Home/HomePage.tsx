import { Skeleton } from '@/components/common/Skeleton'
import { FeaturedMatch } from '@/components/match/FeaturedMatch'
import { MatchListSection } from '@/features/matches/components/MatchListSection'
import {
  useLiveMatches,
  useRecentMatches,
  useUpcomingMatches,
} from '@/features/matches/hooks/useMatches'
import { usePageTitle } from '@/hooks/usePageTitle'

export function HomePage() {
  usePageTitle("Today's cricket")

  const liveQuery = useLiveMatches()
  const upcomingQuery = useUpcomingMatches()
  // A single row of results here; the matches page is where the rest of the archive lives.
  const recentQuery = useRecentMatches(3)

  const featuredMatch = liveQuery.data?.[0]

  return (
    <div className="space-y-8">
      <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">
        Today&rsquo;s cricket
      </h1>

      {/*
        The hero is the first live match, so it only exists when one does. When live cricket is
        empty or unreachable the Live matches section below says so once, rather than twice.
      */}
      {liveQuery.isPending && <Skeleton className="h-56 w-full rounded-2xl" />}
      {featuredMatch && <FeaturedMatch match={featuredMatch} />}

      <MatchListSection
        title="Live matches"
        query={liveQuery}
        emptyTitle="No live matches"
        emptyDescription="Nothing is in play right now. Upcoming fixtures are listed below."
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
    </div>
  )
}
