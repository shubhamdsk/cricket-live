import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { SeriesDetails } from '@/features/series/types'
import { SeriesDetailsPage } from '@/pages/Series/SeriesDetailsPage'
import { match, side } from '@/test/fixtures'
import { renderPage } from '@/test/renderPage'

const slug = 'west-indies-tour-of-india'

function details(overrides: Partial<SeriesDetails> = {}): SeriesDetails {
  return {
    series: {
      id: slug,
      slug,
      name: 'West Indies tour of India',
      startTimeUtc: '2026-09-27T08:00:00Z',
      lastMatchUtc: '2026-10-04T08:00:00Z',
      matchCount: 1,
      totalMatchCount: 3,
      isOngoing: true,
    },
    matches: [
      match({ id: 'a', slug: 'a', matchTitle: '1st ODI' }),
      match({
        id: 'b',
        slug: 'b',
        matchTitle: '2nd ODI',
        status: 'upcoming',
        home: side('India', 'IND'),
        away: side('West Indies', 'WI'),
      }),
      match({
        id: 'c',
        slug: 'c',
        matchTitle: '3rd ODI',
        status: 'upcoming',
        home: side('India', 'IND'),
        away: side('West Indies', 'WI'),
      }),
    ],
    standings: [],
    ...overrides,
  }
}

function open(data: SeriesDetails | null, status?: number) {
  return renderPage('/series/:slug', `/series/${slug}`, <SeriesDetailsPage />, {
    [`/api/series/${slug}`]: { status, data },
  })
}

describe('SeriesDetailsPage', () => {
  it('names the series, marks it ongoing and lists every match', async () => {
    open(details())

    expect(
      await screen.findByRole('heading', { level: 1, name: 'West Indies tour of India' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Ongoing')).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: /\d(st|nd|rd) ODI/ })).toHaveLength(3)
  })

  it('counts the list shown, and says how many of those have scores', async () => {
    open(details())

    expect(
      await screen.findByText('3 matches, 1 with scores recorded here.'),
    ).toBeInTheDocument()
  })

  it('omits the points table when no source supplied one', async () => {
    open(details())

    await screen.findByRole('heading', { level: 1 })
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('explains a series with no fixtures published yet', async () => {
    open(details({ matches: [] }))

    expect(await screen.findByText('No matches listed yet')).toBeInTheDocument()
  })

  it('explains a series nobody knows of', async () => {
    open(null, 404)

    expect(await screen.findByText('Series not found')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Browse series' })).toHaveAttribute(
      'href',
      '/series',
    )
  })
})
