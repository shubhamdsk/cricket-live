import { useInfiniteQuery, useQuery } from '@tanstack/react-query'

import {
  getLiveMatches,
  getMatchDetails,
  getRecentMatches,
  getUpcomingMatches,
} from '@/features/matches/api/matchesApi'
import type { MatchDetails } from '@/features/matches/types'

/**
 * Refetch intervals are set against what the data can actually do, not against how live the page
 * feels. Our API caches a provider response for five minutes and the provider's own free data runs
 * a few minutes behind regardless, so polling faster than this buys nothing. Sprint 5 replaces the
 * interval on live data with an SSE push.
 */
const MINUTE = 60_000

export const matchKeys = {
  all: ['matches'] as const,
  live: () => [...matchKeys.all, 'live'] as const,
  upcoming: () => [...matchKeys.all, 'upcoming'] as const,
  recent: (pageSize: number) => [...matchKeys.all, 'recent', pageSize] as const,
  details: (slug: string) => [...matchKeys.all, 'details', slug] as const,
}

export function useLiveMatches() {
  return useQuery({
    queryKey: matchKeys.live(),
    queryFn: ({ signal }) => getLiveMatches(signal),
    staleTime: MINUTE,
    refetchInterval: MINUTE,
    // Coming back to the tab is the moment a stale score is most obvious.
    refetchOnWindowFocus: true,
  })
}

export function useUpcomingMatches() {
  return useQuery({
    queryKey: matchKeys.upcoming(),
    queryFn: ({ signal }) => getUpcomingMatches(signal),
    // A fixture list changes when a match starts, not minute to minute.
    staleTime: 5 * MINUTE,
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
export function useRecentMatches(pageSize = 12) {
  return useInfiniteQuery({
    queryKey: matchKeys.recent(pageSize),
    queryFn: ({ pageParam, signal }) => getRecentMatches(pageParam, pageSize, signal),
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
