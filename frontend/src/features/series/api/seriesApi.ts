import type { Series, SeriesDetails } from '@/features/series/types'
import { apiGet } from '@/services/apiClient'
import { endpoints } from '@/services/endpoints'

export function getAllSeries(signal?: AbortSignal): Promise<Series[]> {
  return apiGet<Series[]>(endpoints.series.all(), signal)
}

/** Accepts the readable slug or the bare series id; the API resolves either. */
export function getSeriesDetails(
  idOrSlug: string,
  signal?: AbortSignal,
): Promise<SeriesDetails> {
  return apiGet<SeriesDetails>(endpoints.series.details(idOrSlug), signal)
}
