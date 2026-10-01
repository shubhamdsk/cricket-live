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

  // The total, when the index gave us one, because opening the series now shows every match it
  // has — the provider's fixture list fills in the ones we hold no record of. Saying "2 of 8 held"
  // here would describe our records rather than the page the reader is about to see.
  if (total !== null) {
    return `${total} ${plural(total)}`
  }

  // No total means the index did not cover this series, so our own records are all we can speak
  // to, and "held" is the honest word for them.
  return held === 0 ? 'No matches held yet' : `${held} ${plural(held)} held`
}

function plural(count: number): string {
  return count === 1 ? 'match' : 'matches'
}
