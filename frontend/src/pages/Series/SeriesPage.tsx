import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { SeriesCard } from '@/features/series/components/SeriesCard'
import { useAllSeries } from '@/features/series/hooks/useSeries'
import { usePageMeta } from '@/hooks/usePageMeta'

export function SeriesPage() {
  usePageMeta('Series', {
    description:
      'Cricket series and tournaments, recent and upcoming, each with its full schedule of matches.',
  })

  const { data, isPending, isError, error, refetch } = useAllSeries()

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">Series</h1>
        {/*
          No longer explains a shortfall, because there mostly isn't one: a series page lists the
          provider's fixtures as well as the matches recorded here. What it does say is which of
          those two a reader is looking at, since only one of them carries a score.
        */}
        <p className="text-sm text-ink-subtle">
          Recent and upcoming series, each listing its full schedule. Scores appear for matches
          played since this site started recording them.
        </p>
      </div>

      {isPending && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 3 }, (_, index) => (
            <Skeleton key={index} className="h-28 w-full rounded-xl" />
          ))}
        </div>
      )}

      {isError && (
        <ErrorState
          description={error.message}
          onRetry={() => {
            void refetch()
          }}
        />
      )}

      {data !== undefined &&
        (data.length === 0 ? (
          <EmptyState
            title="No series to show"
            description="This fills in from the provider's series list and from the matches recorded here, so an empty page means neither could be read just now."
          />
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {data.map((series) => (
              <SeriesCard key={series.id} series={series} />
            ))}
          </div>
        ))}
    </div>
  )
}
