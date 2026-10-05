import type { UseQueryResult } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'

import { MatchListSection } from '@/features/matches/components/MatchListSection'
import type { DatedMatches, Match } from '@/features/matches/types'
import { match, side } from '@/test/fixtures'

type Query<T> = UseQueryResult<T, Error>

function query<T>(state: Partial<Query<T>>): Query<T> {
  return {
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...state,
  } as Query<T>
}

function renderSection(q: Query<Match[]> | Query<DatedMatches>) {
  return render(
    <MemoryRouter>
      <MatchListSection title="Recent results" query={q} emptyTitle="No results yet" />
    </MemoryRouter>,
  )
}

describe('MatchListSection', () => {
  it('shows each side against its own score', () => {
    renderSection(query<Match[]>({ data: [match()] }))

    const card = screen.getByRole('link', { name: /3rd ODI/ })
    const rows = within(card).getAllByText(/^(India|West Indies)$/)

    expect(rows.map((row) => row.parentElement?.parentElement?.textContent)).toEqual([
      expect.stringContaining('351/7 (50)'),
      expect.stringContaining('352/5 (48.2)'),
    ])
    expect(card).toHaveAttribute('href', '/match/india-vs-west-indies-m1')
  })

  it('says a side is yet to bat rather than showing an empty score', () => {
    renderSection(
      query<Match[]>({ data: [match({ status: 'live', away: side('West Indies', 'WI') })] }),
    )

    expect(screen.getByText('Yet to bat')).toBeInTheDocument()
  })

  it('labels a list answered from a stored window', () => {
    renderSection(
      query<DatedMatches>({ data: { matches: [match()], asOfUtc: new Date().toISOString() } }),
    )

    expect(screen.getByRole('status')).toHaveTextContent('Scores last updated moments ago')
  })

  it('does not label a current list', () => {
    renderSection(query<DatedMatches>({ data: { matches: [match()] } }))

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('shows the empty state for an empty list', () => {
    renderSection(query<Match[]>({ data: [] }))

    expect(screen.getByText('No results yet')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /ODI/ })).not.toBeInTheDocument()
  })

  it('shows the error and retries on request', async () => {
    const refetch = vi.fn()
    renderSection(
      query<Match[]>({ isError: true, error: new Error('Unable to reach the API.'), refetch }),
    )

    expect(screen.getByRole('alert')).toHaveTextContent('Unable to reach the API.')

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }))
    expect(refetch).toHaveBeenCalledOnce()
  })
})
