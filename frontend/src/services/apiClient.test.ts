import { describe, expect, it, vi } from 'vitest'

import { ApiError, apiGet, apiGetDated, apiUrl } from '@/services/apiClient'

function respondWith(body: unknown, status = 200) {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(
      new Response(typeof body === 'string' ? body : JSON.stringify(body), { status }),
    )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('apiUrl', () => {
  it('resolves a path against the configured API', () => {
    expect(apiUrl('/api/matches/live')).toBe('https://api.test/api/matches/live')
  })
})

describe('apiGet', () => {
  it('unwraps the data from a successful envelope', async () => {
    const fetchMock = respondWith({ success: true, message: 'Success', data: [1, 2] })

    await expect(apiGet<number[]>('/api/x')).resolves.toEqual([1, 2])
    expect(fetchMock).toHaveBeenCalledWith(
      'https://api.test/api/x',
      expect.objectContaining({ headers: { Accept: 'application/json' } }),
    )
  })

  it('carries the API message and errors on a failed envelope', async () => {
    respondWith(
      { success: false, message: 'Match not found.', errors: ['id: unknown'], data: null },
      404,
    )

    const error = await apiGet('/api/x').catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      message: 'Match not found.',
      status: 404,
      errors: ['id: unknown'],
    })
  })

  it('treats a 200 that says it failed as a failure', async () => {
    respondWith({ success: false, message: 'No.', data: null })

    await expect(apiGet('/api/x')).rejects.toMatchObject({ message: 'No.', status: 200 })
  })

  it('reports an unreadable body rather than a JSON parse error', async () => {
    respondWith('<html>Bad gateway</html>', 502)

    await expect(apiGet('/api/x')).rejects.toMatchObject({
      message: 'The API returned an unreadable response.',
      status: 502,
    })
  })

  it('reports a body that is JSON but not our envelope', async () => {
    respondWith({ hello: 'world' })

    await expect(apiGet('/api/x')).rejects.toMatchObject({
      message: 'The API returned an unexpected response.',
    })
  })

  it('turns a network failure into a status-0 ApiError', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    await expect(apiGet('/api/x')).rejects.toMatchObject({
      message: 'Unable to reach the Cricket Live API.',
      status: 0,
    })
  })

  it('lets an abort through untouched, so cancelled queries are not shown as errors', async () => {
    const abort = new DOMException('Aborted', 'AbortError')
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(abort))

    await expect(apiGet('/api/x')).rejects.toBe(abort)
  })
})

describe('apiGetDated', () => {
  it('returns the capture time when the API answered from a stored window', async () => {
    respondWith({
      success: true,
      message: 'Success',
      data: [],
      asOfUtc: '2026-10-05T10:00:00Z',
    })

    await expect(apiGetDated('/api/x')).resolves.toEqual({
      data: [],
      asOfUtc: '2026-10-05T10:00:00Z',
    })
  })

  it('leaves the capture time absent when the data is current', async () => {
    respondWith({ success: true, message: 'Success', data: [] })

    await expect(apiGetDated('/api/x')).resolves.toEqual({ data: [], asOfUtc: undefined })
  })
})
