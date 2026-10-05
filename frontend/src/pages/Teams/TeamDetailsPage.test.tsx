import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import type { TeamDetails } from '@/features/teams/types'
import { TeamDetailsPage } from '@/pages/Teams/TeamDetailsPage'
import { match } from '@/test/fixtures'
import { renderPage } from '@/test/renderPage'

function details(overrides: Partial<TeamDetails> = {}): TeamDetails {
  return {
    team: {
      id: 'west-indies',
      name: 'West Indies',
      shortName: 'WI',
      logoUrl: null,
      matchCount: 2,
      firstMatchUtc: '2026-09-27T08:00:00Z',
      lastMatchUtc: '2026-10-04T08:00:00Z',
      isActive: false,
    },
    matches: [
      match({ id: 'a', slug: 'a' }),
      match({ id: 'b', slug: 'b', matchTitle: '2nd ODI' }),
    ],
    series: [
      {
        id: 's',
        slug: 'west-indies-tour-of-india',
        name: 'West Indies tour of India',
        matchCount: 2,
      },
    ],
    opponents: [
      { id: 'india', name: 'India', matchCount: 2 },
      { id: 'nepal', name: 'Nepal', matchCount: 1 },
    ],
    formats: [{ format: 'ODI', matchCount: 2 }],
    ...overrides,
  }
}

function open(data: TeamDetails | null, status?: number) {
  return renderPage('/teams/:slug', '/teams/west-indies', <TeamDetailsPage />, {
    '/api/teams/west-indies': { status, data },
  })
}

describe('TeamDetailsPage', () => {
  it('names the side and counts what is held, by format', async () => {
    open(details())

    expect(
      await screen.findByRole('heading', { level: 1, name: 'West Indies' }),
    ).toBeInTheDocument()
    expect(screen.getByText(/2 matches held here/)).toHaveTextContent('· 2 ODI')
    expect(screen.queryByText('Playing')).not.toBeInTheDocument()
  })

  it('links each series and opponent, counting meetings without claiming a record', async () => {
    open(details())

    expect(
      await screen.findByRole('link', { name: /^West Indies tour of India\s*2$/ }),
    ).toHaveAttribute('href', '/series/west-indies-tour-of-india')
    expect(screen.getByRole('link', { name: /India\s*2 meetings/ })).toHaveAttribute(
      'href',
      '/teams/india',
    )
    expect(screen.getByRole('link', { name: /Nepal\s*1 meeting$/ })).toBeInTheDocument()
  })

  it('marks a side in an unfinished match as playing', async () => {
    open(details({ team: { ...details().team, isActive: true } }))

    expect(await screen.findByText('Playing')).toBeInTheDocument()
  })

  it('explains a side with no matches held', async () => {
    open(null, 404)

    expect(await screen.findByText('Team not found')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'All teams' })).toHaveAttribute('href', '/teams')
  })
})
