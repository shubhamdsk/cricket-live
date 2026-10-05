import { describe, expect, it } from 'vitest'

import { heldCount } from '@/features/series/held'
import type { Series } from '@/features/series/types'

function series(matchCount: number, totalMatchCount: number | null): Series {
  return { matchCount, totalMatchCount } as Series
}

describe('heldCount', () => {
  it('uses the total when the index gave one', () => {
    expect(heldCount(series(2, 8))).toBe('8 matches')
  })

  it('says "match" for a one-match series', () => {
    expect(heldCount(series(1, 1))).toBe('1 match')
  })

  it('speaks only for our own records when there is no total', () => {
    expect(heldCount(series(3, null))).toBe('3 matches held')
  })

  it('says nothing is held rather than "0 matches"', () => {
    expect(heldCount(series(0, null))).toBe('No matches held yet')
  })
})
