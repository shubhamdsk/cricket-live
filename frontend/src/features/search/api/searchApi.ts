import type { SearchResults } from '@/features/search/types'
import { apiGet } from '@/services/apiClient'
import { endpoints } from '@/services/endpoints'

export function search(term: string, signal?: AbortSignal): Promise<SearchResults> {
  return apiGet<SearchResults>(endpoints.search(term), signal)
}
