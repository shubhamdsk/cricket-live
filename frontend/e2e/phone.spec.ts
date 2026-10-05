import { expect, test, type Page } from '@playwright/test'

import { apiBaseUrl } from '../playwright.config'

const odi = {
  id: 'da91f633-acac-4449-86f8-9bff6244053f',
  slug: 'india-vs-west-indies-da91f633-acac-4449-86f8-9bff6244053f',
  status: 'completed',
  format: 'ODI',
  seriesId: 'west-indies-tour-of-india',
  seriesName: 'West Indies tour of India',
  matchTitle: '3rd ODI',
  venue: 'Narendra Modi Stadium, Ahmedabad',
  startTimeUtc: '2026-10-04T08:00:00Z',
  home: {
    team: { id: 'india', name: 'India', shortName: 'IND', logoUrl: null },
    innings: [{ number: 1, runs: 351, wickets: 7, overs: '50' }],
  },
  away: {
    team: { id: 'west-indies', name: 'West Indies', shortName: 'WI', logoUrl: null },
    innings: [{ number: 1, runs: 352, wickets: 5, overs: '48.2' }],
  },
  statusText: 'West Indies won by 5 wkts',
}

// The site and the API are different origins, so a mocked answer still needs the header the real
// API's CORS policy would send, or the browser discards it.
const cors = { 'access-control-allow-origin': '*' }

function ok(data: unknown) {
  return { status: 200, headers: cors, json: { success: true, message: 'Success', data } }
}

async function mockApi(page: Page) {
  await page.route(`${apiBaseUrl}/**`, async (route) => {
    const path = new URL(route.request().url()).pathname

    if (path === '/api/matches/recent') {
      return route.fulfill(ok({ items: [odi], page: 1, pageSize: 3, total: 1, hasMore: false }))
    }
    if (path === `/api/matches/${odi.slug}` || path === `/api/matches/${odi.id}`) {
      return route.fulfill(
        ok({ ...odi, hasBallByBall: false, hasSquads: false, currentBatters: [] }),
      )
    }
    if (path.endsWith('/scorecard')) {
      return route.fulfill({
        status: 404,
        headers: cors,
        json: { success: false, message: 'No scorecard for this match.', data: null },
      })
    }

    return route.fulfill(ok([]))
  })
}

test.beforeEach(async ({ page }) => {
  await mockApi(page)
})

test('a result card shows each side against its own score', async ({ page }) => {
  await page.goto('/')

  const card = page.getByRole('link', { name: /3rd ODI/ })
  await expect(card).toBeVisible()

  // On a phone the row shows the short name, which is what a reader sees the score beside.
  await expect(card.locator('div', { hasText: /^IND/ }).last()).toContainText('351/7 (50)')
  await expect(card.locator('div', { hasText: /^WI/ }).last()).toContainText('352/5 (48.2)')
})

test('every section is reachable from the tab strip without opening a menu', async ({
  page,
}) => {
  await page.goto('/')

  const nav = page.getByRole('navigation', { name: 'Main' })
  for (const name of ['Home', 'Live', 'Matches', 'Series', 'Teams']) {
    await expect(nav.getByRole('link', { name })).toBeVisible()
  }
})

test('the page does not scroll sideways', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('link', { name: /3rd ODI/ })).toBeVisible()

  const overflow = await page.evaluate(
    () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
  )
  expect(overflow).toBe(0)
})

test('the page background is painted below the fold while the toolbar hides', async ({
  page,
}) => {
  await page.goto('/')

  const layer = await page.evaluate(() => {
    const before = getComputedStyle(document.body, '::before')
    return {
      position: before.position,
      height: parseFloat(before.height),
      viewport: window.innerHeight,
      pageColour: getComputedStyle(document.documentElement).backgroundColor,
      bodyColour: getComputedStyle(document.body).backgroundColor,
    }
  })

  // A fixed layer at least as tall as the viewport, and the page colour on <html>, which is what
  // a mobile browser paints into the strip its address bar uncovers.
  expect(layer.position).toBe('fixed')
  expect(layer.height).toBeGreaterThanOrEqual(layer.viewport)
  expect(layer.pageColour).not.toBe('rgba(0, 0, 0, 0)')
  expect(layer.bodyColour).toBe('rgba(0, 0, 0, 0)')
})

test('opening a card goes to that match', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('link', { name: /3rd ODI/ }).click()

  await expect(page).toHaveURL(`/match/${odi.slug}`)
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('IND vs WI')
})
