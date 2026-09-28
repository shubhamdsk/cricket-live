import type { Match, MatchDetails, TeamSummary } from '@/features/matches/types'

function hoursFromNow(hours: number): string {
  return new Date(Date.now() + hours * 60 * 60 * 1000).toISOString()
}

const teams = {
  ind: { id: 'ind', name: 'India', shortName: 'IND' },
  aus: { id: 'aus', name: 'Australia', shortName: 'AUS' },
  eng: { id: 'eng', name: 'England', shortName: 'ENG' },
  rsa: { id: 'rsa', name: 'South Africa', shortName: 'SA' },
  nzl: { id: 'nzl', name: 'New Zealand', shortName: 'NZ' },
  pak: { id: 'pak', name: 'Pakistan', shortName: 'PAK' },
  ban: { id: 'ban', name: 'Bangladesh', shortName: 'BAN' },
  sri: { id: 'sri', name: 'Sri Lanka', shortName: 'SL' },
  win: { id: 'win', name: 'West Indies', shortName: 'WI' },
} satisfies Record<string, TeamSummary>

export const mockMatches: Match[] = [
  {
    id: '1',
    slug: 'ind-vs-aus-3rd-t20i',
    status: 'live',
    format: 'T20',
    seriesName: 'Australia tour of India, 2026',
    matchTitle: '3rd T20I',
    venue: 'Narendra Modi Stadium, Ahmedabad',
    startTimeUtc: hoursFromNow(-2),
    home: { team: teams.ind, innings: [{ runs: 142, wickets: 3, overs: '15.2' }] },
    away: { team: teams.aus, innings: [{ runs: 178, wickets: 6, overs: '20.0' }] },
    statusText: 'India need 37 runs in 28 balls',
  },
  {
    id: '2',
    slug: 'eng-vs-sa-2nd-test',
    status: 'live',
    format: 'TEST',
    seriesName: 'South Africa tour of England, 2026',
    matchTitle: '2nd Test, Day 3',
    venue: "Lord's, London",
    startTimeUtc: hoursFromNow(-5),
    home: {
      team: teams.eng,
      innings: [
        { runs: 341, wickets: 10, overs: '96.4' },
        { runs: 88, wickets: 2, overs: '24.0' },
      ],
    },
    away: { team: teams.rsa, innings: [{ runs: 267, wickets: 10, overs: '81.2' }] },
    statusText: 'England lead by 162 runs',
  },
  {
    id: '3',
    slug: 'nz-vs-pak-1st-odi',
    status: 'upcoming',
    format: 'ODI',
    seriesName: 'Pakistan tour of New Zealand, 2026',
    matchTitle: '1st ODI',
    venue: 'Eden Park, Auckland',
    startTimeUtc: hoursFromNow(19),
    home: { team: teams.nzl, innings: [] },
    away: { team: teams.pak, innings: [] },
    statusText: 'Match starts in 19 hours',
  },
  {
    id: '4',
    slug: 'sl-vs-ban-2nd-t20i',
    status: 'upcoming',
    format: 'T20',
    seriesName: 'Bangladesh tour of Sri Lanka, 2026',
    matchTitle: '2nd T20I',
    venue: 'R. Premadasa Stadium, Colombo',
    startTimeUtc: hoursFromNow(31),
    home: { team: teams.sri, innings: [] },
    away: { team: teams.ban, innings: [] },
    statusText: 'Match starts tomorrow',
  },
  {
    id: '5',
    slug: 'ind-vs-aus-2nd-t20i',
    status: 'completed',
    format: 'T20',
    seriesName: 'Australia tour of India, 2026',
    matchTitle: '2nd T20I',
    venue: 'M. Chinnaswamy Stadium, Bengaluru',
    startTimeUtc: hoursFromNow(-50),
    home: { team: teams.ind, innings: [{ runs: 186, wickets: 5, overs: '19.1' }] },
    away: { team: teams.aus, innings: [{ runs: 183, wickets: 7, overs: '20.0' }] },
    statusText: 'India won by 5 wickets',
  },
  {
    id: '6',
    slug: 'wi-vs-eng-1st-t20i',
    status: 'completed',
    format: 'T20',
    seriesName: 'England tour of West Indies, 2026',
    matchTitle: '1st T20I',
    venue: 'Kensington Oval, Bridgetown',
    startTimeUtc: hoursFromNow(-74),
    home: { team: teams.win, innings: [{ runs: 155, wickets: 9, overs: '20.0' }] },
    away: { team: teams.eng, innings: [{ runs: 172, wickets: 6, overs: '20.0' }] },
    statusText: 'England won by 17 runs',
  },
]

const detailsExtras: Partial<Record<string, Omit<MatchDetails, keyof Match>>> = {
  'ind-vs-aus-3rd-t20i': {
    tossText: 'Australia won the toss and elected to bat',
    summary:
      'Australia posted 178 for 6 on a true surface. India are ahead of the required rate with seven wickets in hand.',
    currentBatters: [
      {
        playerId: 'p1',
        name: 'Shubman Gill',
        runs: 58,
        balls: 37,
        fours: 6,
        sixes: 2,
        isOnStrike: true,
      },
      {
        playerId: 'p2',
        name: 'Suryakumar Yadav',
        runs: 31,
        balls: 18,
        fours: 2,
        sixes: 2,
        isOnStrike: false,
      },
    ],
    currentBowler: {
      playerId: 'p3',
      name: 'Adam Zampa',
      overs: '3.2',
      maidens: 0,
      runs: 28,
      wickets: 1,
    },
  },
  'eng-vs-sa-2nd-test': {
    tossText: 'England won the toss and elected to bat',
    summary:
      'England closed a 74-run first-innings lead and have extended it past 160 with eight second-innings wickets standing.',
    currentBatters: [
      {
        playerId: 'p4',
        name: 'Joe Root',
        runs: 41,
        balls: 78,
        fours: 4,
        sixes: 0,
        isOnStrike: true,
      },
      {
        playerId: 'p5',
        name: 'Harry Brook',
        runs: 22,
        balls: 31,
        fours: 3,
        sixes: 0,
        isOnStrike: false,
      },
    ],
    currentBowler: {
      playerId: 'p6',
      name: 'Kagiso Rabada',
      overs: '9.0',
      maidens: 3,
      runs: 24,
      wickets: 1,
    },
  },
}

export const mockMatchDetails: MatchDetails[] = mockMatches.map((match) => ({
  ...match,
  tossText: null,
  summary: null,
  currentBatters: [],
  currentBowler: null,
  ...detailsExtras[match.slug],
}))
