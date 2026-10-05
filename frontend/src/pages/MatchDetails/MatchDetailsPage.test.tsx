import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { MatchDetails } from '@/features/matches/types'
import { MatchDetailsPage } from '@/pages/MatchDetails/MatchDetailsPage'
import { match, side } from '@/test/fixtures'
import { renderPage } from '@/test/renderPage'

function details(overrides: Partial<MatchDetails> = {}): MatchDetails {
  return {
    ...match(),
    hasBallByBall: false,
    hasSquads: false,
    currentBatters: [],
    ...overrides,
  }
}

function open(data: MatchDetails | null, status?: number) {
  return renderPage('/match/:slug', `/match/${details().slug}`, <MatchDetailsPage />, {
    [`/api/matches/${details().slug}`]: { status, data },
  })
}

describe('MatchDetailsPage', () => {
  it('credits each innings to the side that batted it', async () => {
    open(details())

    const innings = await screen.findByRole('heading', { name: 'Innings' })
    const rows = within(innings.closest('div')!).getAllByRole('term')

    expect(rows.map((row) => [row.textContent, row.nextElementSibling?.textContent])).toEqual([
      ['India 1st innings', '351/7 (50)'],
      ['West Indies 1st innings', '352/5 (48.2)'],
    ])
  })

  it('links the series and both teams to their own pages', async () => {
    open(details())

    expect(
      await screen.findByRole('link', { name: 'West Indies tour of India' }),
    ).toHaveAttribute('href', '/series/west-indies-tour-of-india')
    expect(screen.getByRole('link', { name: 'India' })).toHaveAttribute('href', '/teams/india')
    expect(screen.getByRole('link', { name: 'West Indies' })).toHaveAttribute(
      'href',
      '/teams/west-indies',
    )
  })

  it('says no innings yet before the first ball', async () => {
    open(
      details({
        status: 'upcoming',
        home: side('India', 'IND'),
        away: side('West Indies', 'WI'),
      }),
    )

    expect(await screen.findByText('No innings yet')).toBeInTheDocument()
  })

  it('shows the batters at the crease only when the source knows them', async () => {
    open(
      details({
        status: 'completed',
        currentBatters: [{ name: 'S Hope', runs: 88, balls: 71 }],
      }),
    )

    expect(await screen.findByRole('heading', { name: 'At the crease' })).toBeInTheDocument()
    expect(screen.getByText('S Hope')).toBeInTheDocument()
  })

  it('explains a match the API does not hold', async () => {
    open(null, 404)

    expect(await screen.findByText('Match not found')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to home' })).toHaveAttribute('href', '/')
  })

  it('offers a retry when the API fails', async () => {
    open(null, 503)

    expect(await screen.findByRole('alert')).toHaveTextContent('Not found.')
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })
})
