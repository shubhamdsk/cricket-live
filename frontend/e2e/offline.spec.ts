import { expect, test } from '@playwright/test'

import { apiBaseUrl } from '../playwright.config'

test.use({ serviceWorkers: 'allow' })

const result = {
  id: 'm1',
  slug: 'india-vs-west-indies-m1',
  status: 'completed',
  format: 'ODI',
  seriesId: 's',
  seriesName: 'West Indies tour of India',
  matchTitle: '3rd ODI',
  venue: 'Ahmedabad',
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

test('the last scores this device saw are still readable offline', async ({
  page,
  context,
}) => {
  // Context-level, because with the worker running it is the worker that makes these requests.
  await context.route(`${apiBaseUrl}/**`, (route) => {
    const path = new URL(route.request().url()).pathname
    const data =
      path === '/api/matches/recent'
        ? { items: [result], page: 1, pageSize: 3, total: 1, hasMore: false }
        : []

    return route.fulfill({
      headers: { 'access-control-allow-origin': '*' },
      json: { success: true, message: 'Success', data },
    })
  })

  await page.goto('/')
  await page.evaluate(async () => {
    await navigator.serviceWorker.ready
  })

  // Once controlled, load again so the worker sees and keeps the API answers.
  await page.reload()
  await expect(page.getByRole('link', { name: /3rd ODI/ })).toBeVisible()
  await expect
    .poll(() =>
      page.evaluate(async () => (await caches.keys()).some((name) => name.startsWith('api-'))),
    )
    .toBe(true)

  await context.setOffline(true)
  await page.reload()

  await expect(page.getByText('You’re offline')).toBeVisible()
  await expect(page.getByRole('link', { name: /3rd ODI/ })).toContainText('352/5')
})
