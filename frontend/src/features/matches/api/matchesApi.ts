import { mockMatchDetails, mockMatches } from '@/features/matches/mocks/matches'
import type { Match, MatchDetails } from '@/features/matches/types'
import { ApiError } from '@/services/apiClient'

/**
 * Sprint 2 serves mock data. Sprint 3 replaces these bodies with `apiGet` calls
 * against the .NET API; the signatures and return types stay as they are.
 */
const MOCK_LATENCY_MS = 350

function resolveAfterDelay<T>(value: T, signal?: AbortSignal): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const abortError = () => new DOMException('The operation was aborted.', 'AbortError')

    if (signal?.aborted) {
      reject(abortError())
      return
    }

    const timer = setTimeout(() => resolve(value), MOCK_LATENCY_MS)

    signal?.addEventListener(
      'abort',
      () => {
        clearTimeout(timer)
        reject(abortError())
      },
      { once: true },
    )
  })
}

export function getLiveMatches(signal?: AbortSignal): Promise<Match[]> {
  return resolveAfterDelay(
    mockMatches.filter((match) => match.status === 'live'),
    signal,
  )
}

export function getUpcomingMatches(signal?: AbortSignal): Promise<Match[]> {
  return resolveAfterDelay(
    mockMatches.filter((match) => match.status === 'upcoming'),
    signal,
  )
}

export function getRecentMatches(signal?: AbortSignal): Promise<Match[]> {
  return resolveAfterDelay(
    mockMatches.filter((match) => match.status === 'completed'),
    signal,
  )
}

export async function getMatchDetails(
  slug: string,
  signal?: AbortSignal,
): Promise<MatchDetails> {
  const match = mockMatchDetails.find((item) => item.slug === slug)

  if (!match) {
    throw new ApiError('Match not found.', 404)
  }

  return resolveAfterDelay(match, signal)
}
