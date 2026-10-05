import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { vi } from 'vitest'

type Answer = { status?: number; data: unknown }

/**
 * Renders one page at a real route, with `fetch` answering from `api` by path.
 *
 * A path not in the table is a 404 in the API's own envelope, so a section that asks for more
 * than the test set up fails the way it would against a real API missing that data.
 */
export function renderPage(
  pattern: string,
  path: string,
  page: ReactElement,
  api: Record<string, Answer>,
) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const { pathname } = new URL(String(input))
    const answer = api[pathname]
    const status = answer?.status ?? (answer ? 200 : 404)
    const success = status < 400

    return new Response(
      JSON.stringify({
        success,
        message: success ? 'Success' : 'Not found.',
        data: success ? answer?.data : null,
      }),
      { status },
    )
  })
  vi.stubGlobal('fetch', fetchMock)

  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

  render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path={pattern} element={page} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )

  return fetchMock
}
