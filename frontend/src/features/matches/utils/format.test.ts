import { afterEach, describe, expect, it, vi } from 'vitest'

import {
  formatAsOf,
  formatInnings,
  formatInningsLabel,
  formatTeamScore,
} from '@/features/matches/utils/format'
import { innings, side } from '@/test/fixtures'

describe('formatInnings', () => {
  it('shows wickets when the side is not all out', () => {
    expect(formatInnings(innings(351, 7, '50'))).toBe('351/7 (50)')
  })

  it('drops the wickets when the side is all out', () => {
    expect(formatInnings(innings(187, 10, '41.3'))).toBe('187 (41.3)')
  })
})

describe('formatTeamScore', () => {
  it('is null for a side yet to bat', () => {
    expect(formatTeamScore(side('India', 'IND'))).toBeNull()
  })

  it('joins two innings the way a Test scorecard does', () => {
    const tested = side('India', 'IND', [innings(250, 10, '80'), innings(120, 3, '30', 2)])

    expect(formatTeamScore(tested)).toBe('250 (80) & 120/3 (30)')
  })
})

describe('formatInningsLabel', () => {
  it.each([
    [1, '1st innings'],
    [2, '2nd innings'],
    [3, '3rd innings'],
    [4, '4th innings'],
    [11, '11th innings'],
    [12, '12th innings'],
    [13, '13th innings'],
    [21, '21st innings'],
    [112, '112th innings'],
  ])('labels innings %i as "%s"', (number, label) => {
    expect(formatInningsLabel(number)).toBe(label)
  })
})

describe('formatAsOf', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  function at(now: string) {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(now))
  }

  it.each([
    ['2026-10-05T11:59:30Z', 'moments ago'],
    ['2026-10-05T11:35:00Z', '25 minutes ago'],
    ['2026-10-05T11:00:00Z', 'about an hour ago'],
    ['2026-10-05T08:50:00Z', 'about 3 hours ago'],
  ])('describes %s as "%s" at noon', (asOf, expected) => {
    at('2026-10-05T12:00:00Z')

    expect(formatAsOf(asOf)).toBe(expected)
  })

  it('falls back to "recently" for a value it cannot read', () => {
    expect(formatAsOf('not a date')).toBe('recently')
  })
})
