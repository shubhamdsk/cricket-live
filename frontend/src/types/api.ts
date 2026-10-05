export interface ApiResponse<T> {
  success: boolean
  data: T | null
  message: string
  errors?: string[]
  /**
   * When the API read this from the score provider, present only when that was not just now.
   *
   * Absent on almost every response, and the absence is the meaning: no field means current. It
   * appears when the API has recovered a stored copy during a provider outage, so a page can say
   * when the data is from instead of presenting an old score as a live one.
   */
  asOfUtc?: string
}
