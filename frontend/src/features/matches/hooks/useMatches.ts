import { useInfiniteQuery, useQuery } from '@tanstack/react-query'

import {
  getLiveMatches,
  getMatchDetails,
  getMatchScorecard,
  getRecentMatches,
  getSeriesNames,
  getUpcomingMatches,
} from '@/features/matches/api/matchesApi'
import type { MatchDetails } from '@/features/matches/types'
import type { MatchFilterParams } from '@/services/endpoints'

/**
 * Refetch intervals are set against what the data can actually do, not against how live the page
 * feels. Our API caches a provider response for five minutes and the provider's own free data runs
 * a few minutes behind regardless, so polling faster than this buys nothing. Sprint 5 replaces the
 * interval on live data with an SSE push.
 */
const MINUTE = 60_000

/**
 * The filter is part of every list key. Leaving it out would let a filtered list be served from
 * the cache of an unfiltered one, which looks like the filter silently failing.
 */
export const matchKeys = {
  all: ['matches'] as const,
  live: (filter: MatchFilterParams) => [...matchKeys.all, 'live', filter] as const,
  upcoming: (filter: MatchFilterParams) => [...matchKeys.all, 'upcoming', filter] as const,
  recent: (pageSize: number, filter: MatchFilterParams) =>
    [...matchKeys.all, 'recent', pageSize, filter] as const,
  series: () => [...matchKeys.all, 'series'] as const,
  details: (slug: string) => [...matchKeys.all, 'details', slug] as const,
  scorecard: (slug: string) => [...matchKeys.all, 'scorecard', slug] as const,
}

export function useLiveMatches(filter: MatchFilterParams = {}) {
  return useQuery({
    queryKey: matchKeys.live(filter),
    queryFn: ({ signal }) => getLiveMatches(filter, signal),
    staleTime: MINUTE,
    refetchInterval: MINUTE,
    // Coming back to the tab is the moment a stale score is most obvious.
    refetchOnWindowFocus: true,
  })
}

export function useUpcomingMatches(filter: MatchFilterParams = {}) {
  return useQuery({
    queryKey: matchKeys.upcoming(filter),
    queryFn: ({ signal }) => getUpcomingMatches(filter, signal),
    // A fixture list changes when a match starts, not minute to minute.
    staleTime: 5 * MINUTE,
  })
}

/**
 * The series a reader can filter by.
 *
 * Long stale time because which tournaments exist changes over days, while their scores change
 * over minutes — and this is read on every visit to the matches page.
 */
export function useSeriesNames() {
  return useQuery({
    queryKey: matchKeys.series(),
    queryFn: ({ signal }) => getSeriesNames(signal),
    staleTime: 30 * MINUTE,
  })
}

/**
 * Completed matches, newest first, in pages.
 *
 * Results are the one list that grows without bound: unlike live and upcoming, they come from what
 * the API kept rather than from the provider's few-day window, so there is no point at which the
 * list is naturally short. Infinite rather than numbered pages because nobody navigates results by
 * page number — they scroll until they find the match they remember.
 */
export function useRecentMatches(pageSize = 12, filter: MatchFilterParams = {}) {
  return useInfiniteQuery({
    queryKey: matchKeys.recent(pageSize, filter),
    queryFn: ({ pageParam, signal }) => getRecentMatches(pageParam, pageSize, filter, signal),
    initialPageParam: 1,
    // hasMore comes from the API, which knows the total; guessing from a short page would stop
    // early the moment an unreadable row is dropped from one.
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
    staleTime: 5 * MINUTE,
  })
}

export function useMatchDetails(slug: string | undefined) {
  return useQuery({
    queryKey: matchKeys.details(slug ?? ''),
    queryFn: ({ signal }) => getMatchDetails(slug!, signal),
    enabled: Boolean(slug),
    staleTime: MINUTE,
    // A finished match cannot change, so stop asking.
    refetchInterval: (query) => {
      const match = query.state.data as MatchDetails | undefined
      return match?.status === 'live' ? MINUTE : false
    },
    refetchOnWindowFocus: true,
  })
}

/**
 * The full card, fetched only once someone asks to see it.
 *
 * Everything unusual about this hook comes from the allowance behind the endpoint, which is two
 * hundred requests a month rather than a day. So: `enabled` is the reader's choice rather than
 * merely "is there a slug", there is no refetch interval at all, and the stale time is long
 * enough that opening and closing the section a few times costs one request.
 *
 * It never retries. A failure here has already spent a request, and the usual reason for one is
 * that the allowance is gone — which asking again cannot fix and does make worse.
 */
export function useMatchScorecard(slug: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: matchKeys.scorecard(slug ?? ''),
    queryFn: ({ signal }) => getMatchScorecard(slug!, signal),
    enabled: Boolean(slug) && enabled,
    staleTime: 5 * MINUTE,
    // A completed card cannot change, and a live one is served from a server-side cache of the
    // same length, so refetching on focus would spend a request to be handed what we already hold.
    refetchOnWindowFocus: false,
    retry: false,
  })
}
