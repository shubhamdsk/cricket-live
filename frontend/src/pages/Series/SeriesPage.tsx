import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { SeriesCard } from '@/features/series/components/SeriesCard'
import { useAllSeries } from '@/features/series/hooks/useSeries'
import { usePageTitle } from '@/hooks/usePageTitle'

export function SeriesPage() {
  usePageTitle('Series')

  const { data, isPending, isError, error, refetch } = useAllSeries()

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">Series</h1>
        {/*
          Said plainly rather than implied, and the two halves are deliberately separate: the
          provider's index decides what is listed, our own matches decide what a series page can
          show. Conflating them is what made this page read as broken when the list came only
          from the window.
        */}
        <p className="text-sm text-ink-subtle">
          Recent and upcoming series. Each card says how many of its matches we hold — we keep
          the ones played since this site started, so an older tournament will show fewer than
          it played.
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
