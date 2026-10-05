import { EmptyState } from '@/components/common/EmptyState'
import { LoadMore } from '@/features/matches/components/LoadMore'
import { MatchFilterBar } from '@/features/matches/components/MatchFilterBar'
import { MatchListSection } from '@/features/matches/components/MatchListSection'
import { useMatchFilters } from '@/features/matches/hooks/useMatchFilters'
import {
  useLiveMatches,
  useRecentMatches,
  useSeriesNames,
  useUpcomingMatches,
} from '@/features/matches/hooks/useMatches'
import { usePageMeta } from '@/hooks/usePageMeta'

export function MatchesPage() {
  usePageMeta('Matches', {
    description:
      'Cricket matches live, upcoming and completed, filterable by series, format and team.',
  })

  const filterState = useMatchFilters()
  const { filters, params, isFiltered } = filterState

  const seriesQuery = useSeriesNames()
  const liveQuery = useLiveMatches(params)
  const upcomingQuery = useUpcomingMatches(params)
  const recentQuery = useRecentMatches(12, params)

  const pages = recentQuery.data?.pages ?? []
  const shown = pages.reduce((count, page) => count + page.items.length, 0)

  // A status filter is a request to see one list, so the other two are hidden rather than shown
  // empty. Three "no matches" panels are not an answer to "show me what is live".
  const shows = (status: 'live' | 'upcoming' | 'completed') =>
    filters.status === 'all' || filters.status === status

  // When a filter excludes everything, say so once and drop the sections entirely. Otherwise the
  // reader gets one banner and three identical empty panels for the same single fact.
  const noneMatch =
    isFiltered &&
    (!shows('live') || liveQuery.data?.matches.length === 0) &&
    (!shows('upcoming') || upcomingQuery.data?.matches.length === 0) &&
    (!shows('completed') || (recentQuery.isSuccess && shown === 0))

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">Matches</h1>
        <p className="text-sm text-ink-subtle">
          Filter by status, date, or series. Completed matches reach back as far as this site
          has been recording them.
        </p>
      </div>

      <MatchFilterBar
        state={filterState}
        series={seriesQuery.data ?? []}
        isSeriesPending={seriesQuery.isPending}
      />

      {noneMatch && (
        <EmptyState
          title="No matches fit this filter"
          description="Try a wider date, another series, or clear the filters to see everything."
        />
      )}

      {!noneMatch && shows('live') && (
        <MatchListSection title="Live" query={liveQuery} emptyTitle="No live matches" />
      )}

      {!noneMatch && shows('upcoming') && (
        <MatchListSection
          title="Upcoming"
          query={upcomingQuery}
          emptyTitle="No upcoming matches"
        />
      )}

      {!noneMatch && shows('completed') && (
        <MatchListSection
          title="Completed"
          query={recentQuery}
          emptyTitle="No completed matches"
          emptyDescription={
            isFiltered
              ? 'Nothing recorded matches this filter.'
              : 'Results are kept from the day this site started recording them, so this fills in as matches finish.'
          }
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
      )}
    </div>
  )
}
