import type { Series } from '@/features/series/types'

/**
 * How much of a series we can show, said so that a small number is not mistaken for a short
 * series.
 *
 * Three readings, because there are three things we might know: nothing held, held with a total
 * to compare against, and held without one. `totalMatchCount` of `null` means the provider's
 * index did not cover the series, which is not the same as the series having no matches.
 *
 * Shared by the card and the details page so the two cannot drift into describing the same
 * numbers differently.
 */
export function heldCount(series: Series): string {
  const { matchCount: held, totalMatchCount: total } = series

  if (total === null) {
    return held === 0 ? 'No matches held yet' : `${held} ${plural(held)} held`
  }

  // The total agrees with the noun, not the held count: "1 of 1 match held", and a one-match
  // series does exist — the provider lists tours with a single fixture.
  return held === 0
    ? `None of ${total} ${plural(total)} held yet`
    : `${held} of ${total} ${plural(total)} held`
}

function plural(count: number): string {
  return count === 1 ? 'match' : 'matches'
}
