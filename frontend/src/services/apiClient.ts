import type { ApiResponse } from '@/types/api'

const baseUrl = import.meta.env.VITE_API_BASE_URL

/**
 * Turns a path from `endpoints` into the URL to call.
 *
 * Exported because the live stream is opened by `EventSource` rather than `fetch` and so cannot go
 * through `apiGet`, but must still resolve against the same API. This is the only place the base
 * URL is read.
 */
export function apiUrl(path: string): string {
  return `${baseUrl}${path}`
}

export class ApiError extends Error {
  readonly status: number
  readonly errors?: string[]

  constructor(message: string, status: number, errors?: string[]) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.errors = errors
  }
}

function isApiResponse(value: unknown): value is ApiResponse<unknown> {
  return typeof value === 'object' && value !== null && 'success' in value && 'message' in value
}

/**
 * A successful response with the part of the envelope that is not the data.
 *
 * Only `asOfUtc` so far, and only two endpoints read it, which is why this is a second function
 * rather than the shape `apiGet` returns. Thirty-odd call sites unwrapping a `.data` to reach a
 * field that two of them use would be a worse trade than one extra export.
 */
export interface Dated<T> {
  data: T
  asOfUtc?: string
}

export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const { data } = await apiGetDated<T>(path, signal)
  return data
}

export async function apiGetDated<T>(path: string, signal?: AbortSignal): Promise<Dated<T>> {
  let response: Response

  try {
    response = await fetch(apiUrl(path), {
      signal,
      headers: { Accept: 'application/json' },
    })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') {
      throw cause
    }

    throw new ApiError('Unable to reach the Cricket Live API.', 0)
  }

  let payload: unknown

  try {
    payload = await response.json()
  } catch {
    throw new ApiError('The API returned an unreadable response.', response.status)
  }

  if (!isApiResponse(payload)) {
    throw new ApiError('The API returned an unexpected response.', response.status)
  }

  if (!response.ok || !payload.success) {
    throw new ApiError(payload.message, response.status, payload.errors)
  }

  return { data: payload.data as T, asOfUtc: payload.asOfUtc }
}
