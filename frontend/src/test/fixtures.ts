import type { InningsScore, Match, TeamInnings } from '@/features/matches/types'

export function side(
  name: string,
  shortName: string,
  innings: InningsScore[] = [],
): TeamInnings {
  return {
    team: { id: name.toLowerCase().replaceAll(' ', '-'), name, shortName, logoUrl: null },
    innings,
  }
}

export function innings(
  runs: number,
  wickets: number,
  overs: string,
  number = 1,
): InningsScore {
  return { number, runs, wickets, overs }
}

export function match(overrides: Partial<Match> = {}): Match {
  return {
    id: 'm1',
    slug: 'india-vs-west-indies-m1',
    status: 'completed',
    format: 'ODI',
    seriesId: 'west-indies-tour-of-india',
    seriesName: 'West Indies tour of India',
    matchTitle: '3rd ODI',
    venue: 'Ahmedabad',
    startTimeUtc: '2026-10-04T08:00:00Z',
    home: side('India', 'IND', [innings(351, 7, '50')]),
    away: side('West Indies', 'WI', [innings(352, 5, '48.2')]),
    statusText: 'West Indies won by 5 wkts',
    ...overrides,
  }
}
