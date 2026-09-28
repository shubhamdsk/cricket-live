import type { ApiResponse } from '@/types/api'

const baseUrl = import.meta.env.VITE_API_BASE_URL

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

export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response

  try {
    response = await fetch(`${baseUrl}${path}`, {
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

  return payload.data as T
}
