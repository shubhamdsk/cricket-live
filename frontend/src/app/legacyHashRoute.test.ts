import { describe, expect, it } from 'vitest'

import { rewriteLegacyHashRoute } from '@/app/legacyHashRoute'

function visit(url: string) {
  window.history.replaceState(null, '', url)
  rewriteLegacyHashRoute()
  return `${window.location.pathname}${window.location.search}${window.location.hash}`
}

describe('rewriteLegacyHashRoute', () => {
  it('turns an old hash route into a real path', () => {
    expect(visit('/#/match/india-vs-west-indies-m1')).toBe('/match/india-vs-west-indies-m1')
  })

  it('drops a bare "#/" but keeps the query string', () => {
    expect(visit('/?ref=share#/')).toBe('/?ref=share')
  })

  it('leaves a plain anchor alone', () => {
    expect(visit('/#scores')).toBe('/#scores')
  })

  it('leaves a hash on a real route alone', () => {
    expect(visit('/live#/teams')).toBe('/live#/teams')
  })
})
