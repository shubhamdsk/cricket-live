import { beforeEach, describe, expect, it, vi } from 'vitest'

import { reloadForNewVersion } from '@/app/lazyPage'

describe('reloadForNewVersion', () => {
  beforeEach(() => sessionStorage.clear())

  it('reloads the first time a page fails to load', () => {
    const reload = vi.fn()

    expect(reloadForNewVersion(1_000_000, sessionStorage, reload)).toBe(true)
    expect(reload).toHaveBeenCalledOnce()
  })

  it('does not reload again straight after a reload, so a real fault cannot loop', () => {
    const reload = vi.fn()
    reloadForNewVersion(1_000_000, sessionStorage, reload)

    expect(reloadForNewVersion(1_010_000, sessionStorage, reload)).toBe(false)
    expect(reload).toHaveBeenCalledOnce()
  })

  it('reloads again for a later release once the cooldown has passed', () => {
    const reload = vi.fn()
    reloadForNewVersion(1_000_000, sessionStorage, reload)

    expect(reloadForNewVersion(1_060_000, sessionStorage, reload)).toBe(true)
    expect(reload).toHaveBeenCalledTimes(2)
  })
})
