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
          Said plainly rather than implied. A series here is assembled from the matches we hold,
          so a tournament that started before this site did will look shorter than it was.
        */}
        <p className="text-sm text-ink-subtle">
          Built from the matches recorded here, so a tournament shows the matches we have rather
          than everything it played.
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
            title="No series yet"
            description="Series appear as matches are recorded, so this fills in as cricket is played."
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
