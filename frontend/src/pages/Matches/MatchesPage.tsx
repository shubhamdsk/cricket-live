import { LoadMore } from '@/features/matches/components/LoadMore'
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

  const pages = recentQuery.data?.pages ?? []
  const shown = pages.reduce((count, page) => count + page.items.length, 0)

  return (
    <div className="space-y-8">
      <div className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">Matches</h1>
        <p className="text-sm text-ink-subtle">
          Filtering by date and series arrives later in Sprint 7.
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
        emptyDescription="Results are kept from the day this site started recording them, so this fills in as matches finish."
        footer={
          <LoadMore
            hasMore={recentQuery.hasNextPage}
            isLoading={recentQuery.isFetchingNextPage}
            total={pages[0]?.total ?? shown}
            shown={shown}
            onLoadMore={() => void recentQuery.fetchNextPage()}
          />
        }
      />
    </div>
  )
}
