import { useQuery } from '@tanstack/react-query'

import {
  getLiveMatches,
  getMatchDetails,
  getRecentMatches,
  getUpcomingMatches,
} from '@/features/matches/api/matchesApi'

export const matchKeys = {
  all: ['matches'] as const,
  live: () => [...matchKeys.all, 'live'] as const,
  upcoming: () => [...matchKeys.all, 'upcoming'] as const,
  recent: () => [...matchKeys.all, 'recent'] as const,
  details: (slug: string) => [...matchKeys.all, 'details', slug] as const,
}

export function useLiveMatches() {
  return useQuery({
    queryKey: matchKeys.live(),
    queryFn: ({ signal }) => getLiveMatches(signal),
  })
}

export function useUpcomingMatches() {
  return useQuery({
    queryKey: matchKeys.upcoming(),
    queryFn: ({ signal }) => getUpcomingMatches(signal),
  })
}

export function useRecentMatches() {
  return useQuery({
    queryKey: matchKeys.recent(),
    queryFn: ({ signal }) => getRecentMatches(signal),
  })
}

export function useMatchDetails(slug: string | undefined) {
  return useQuery({
    queryKey: matchKeys.details(slug ?? ''),
    queryFn: ({ signal }) => getMatchDetails(slug!, signal),
    enabled: Boolean(slug),
  })
}
