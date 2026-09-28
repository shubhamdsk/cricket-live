import { MatchListSection } from '@/features/matches/components/MatchListSection'
import {
  useLiveMatches,
  useRecentMatches,
  useUpcomingMatches,
} from '@/features/matches/hooks/useMatches'

export function MatchesPage() {
  const liveQuery = useLiveMatches()
  const upcomingQuery = useUpcomingMatches()
  const recentQuery = useRecentMatches()

  return (
    <div className="space-y-8">
      <div className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">Matches</h1>
        <p className="text-sm text-ink-subtle">
          Filtering by date and series arrives in Sprint 7.
        </p>
      </div>

      <MatchListSection title="Live" query={liveQuery} emptyTitle="No live matches" />

      <MatchListSection
        title="Upcoming"
        query={upcomingQuery}
        emptyTitle="No upcoming matches"
      />

      <MatchListSection
        title="Completed"
        query={recentQuery}
        emptyTitle="No completed matches"
      />
    </div>
  )
}
