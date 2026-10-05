import { readFileSync } from 'node:fs'

import { describe, expect, it } from 'vitest'

import { describeMatch, isLinkPreviewBot, withPreview } from '@/share/linkPreview'
import { match, side } from '@/test/fixtures'

describe('isLinkPreviewBot', () => {
  it.each([
    'WhatsApp/2.23.20.0',
    'facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)',
    'Twitterbot/1.0',
    'Slackbot-LinkExpanding 1.0 (+https://api.slack.com/robots)',
    'TelegramBot (like TwitterBot)',
    'Mozilla/5.0 (compatible; Discordbot/2.0; +https://discordapp.com)',
  ])('recognises %s', (agent) => {
    expect(isLinkPreviewBot(agent)).toBe(true)
  })

  it('leaves a phone browser alone', () => {
    expect(
      isLinkPreviewBot(
        'Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 Chrome/129.0 Mobile Safari/537.36',
      ),
    ).toBe(false)
    expect(isLinkPreviewBot(null)).toBe(false)
  })
})

describe('describeMatch', () => {
  it('puts both scores in the title and the result underneath', () => {
    expect(describeMatch(match())).toEqual({
      title: 'IND 351/7 vs WI 352/5 · 3rd ODI',
      description:
        'West Indies won by 5 wkts. India vs West Indies, West Indies tour of India, Ahmedabad.',
    })
  })

  it('gives the start date for a match not yet begun', () => {
    const upcoming = match({
      status: 'upcoming',
      home: side('India', 'IND'),
      away: side('West Indies', 'WI'),
      statusText: '',
    })

    expect(describeMatch(upcoming)).toMatchObject({
      title: 'IND vs WI · 3rd ODI',
      description: expect.stringMatching(/^Starts 4 Oct 2026\./),
    })
  })

  it('says a match is live', () => {
    const live = match({ status: 'live', statusText: 'West Indies need 20 runs' })

    expect(describeMatch(live).description).toMatch(/^Live: West Indies need 20 runs\./)
  })
})

describe('withPreview', () => {
  const html = readFileSync('index.html', 'utf8')
  const preview = {
    title: 'IND 351/7 vs WI 352/5 · 3rd ODI',
    description: 'West Indies won by 5 wkts. India vs "West Indies" <b>',
    url: 'https://cricket-live-shubhamdsk1.vercel.app/match/india-vs-west-indies-m1',
  }

  function meta(page: string, selector: string) {
    return new DOMParser().parseFromString(page, 'text/html').querySelector(selector)
  }

  it('replaces every tag a link unfurler reads', () => {
    const page = withPreview(html, preview)
    const title = 'IND 351/7 vs WI 352/5 · 3rd ODI | Cricket Live'

    expect(meta(page, 'title')?.textContent).toBe(title)
    expect(meta(page, 'meta[property="og:title"]')?.getAttribute('content')).toBe(title)
    expect(meta(page, 'meta[name="twitter:title"]')?.getAttribute('content')).toBe(title)
    expect(meta(page, 'meta[property="og:url"]')?.getAttribute('content')).toBe(preview.url)
    expect(meta(page, 'link[rel="canonical"]')?.getAttribute('href')).toBe(preview.url)
    expect(meta(page, 'meta[property="og:type"]')?.getAttribute('content')).toBe('article')
  })

  it('escapes what it inserts, so a team name cannot break out of the attribute', () => {
    const page = withPreview(html, preview)

    expect(meta(page, 'meta[property="og:description"]')?.getAttribute('content')).toBe(
      preview.description,
    )
    expect(page).not.toContain('<b>')
  })

  it('leaves the app itself untouched', () => {
    const page = withPreview(html, preview)

    expect(page).toContain('<div id="root"></div>')
    expect(page).toContain('/src/main.tsx')
  })
})
