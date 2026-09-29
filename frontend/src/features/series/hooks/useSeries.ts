import { useQuery } from '@tanstack/react-query'

import { getAllSeries, getSeriesDetails } from '@/features/series/api/seriesApi'

const MINUTE = 60_000

export const seriesKeys = {
  all: ['series'] as const,
  list: () => [...seriesKeys.all, 'list'] as const,
  details: (idOrSlug: string) => [...seriesKeys.all, 'details', idOrSlug] as const,
}

/**
 * Which tournaments exist changes over days, not minutes, so this is held for a long time.
 * The scores inside them are a different question and are refetched on the pages that show them.
 */
export function useAllSeries() {
  return useQuery({
    queryKey: seriesKeys.list(),
    queryFn: ({ signal }) => getAllSeries(signal),
    staleTime: 30 * MINUTE,
  })
}

/**
 * Refetched while the series has a match in progress, and left alone once it does not.
 *
 * A finished tournament cannot change, and the points table behind this is read from a site that
 * did not ask for the traffic, so polling one that has stopped moving would be pure cost.
 */
export function useSeriesDetails(idOrSlug: string | undefined) {
  return useQuery({
    queryKey: seriesKeys.details(idOrSlug ?? ''),
    queryFn: ({ signal }) => getSeriesDetails(idOrSlug!, signal),
    enabled: Boolean(idOrSlug),
    staleTime: 5 * MINUTE,
    refetchInterval: (query) => (query.state.data?.series.isOngoing ? 5 * MINUTE : false),
  })
}
