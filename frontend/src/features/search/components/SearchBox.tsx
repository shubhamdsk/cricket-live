import { Search, X } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { SearchResultGroups } from '@/features/search/components/SearchResultGroups'
import { MIN_QUERY_LENGTH, useSearch } from '@/features/search/hooks/useSearch'
import { cn } from '@/utils/cn'

/**
 * The header search: type, see results, follow one.
 *
 * The panel is not a listbox and the input is not a combobox, deliberately. A real combobox owes
 * the reader arrow-key traversal, `aria-activedescendant` and managed focus, and a half-built one
 * announces itself as something it cannot do. This is an input with a group of links beneath it,
 * which is what it behaves like: Tab reaches each result, Escape closes, Enter opens the full
 * results page.
 */
export function SearchBox({ onNavigate }: { onNavigate?: () => void }) {
  const [term, setTerm] = useState('')
  const [open, setOpen] = useState(false)
  const container = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()

  const { data, isFetching, settled, enabled } = useSearch(term)

  // Closes when focus or a click leaves the box entirely. `focusout` rather than `blur` because
  // moving from the input to a result inside the panel must not count as leaving.
  useEffect(() => {
    function onDocumentInteraction(event: Event) {
      if (!container.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }

    document.addEventListener('pointerdown', onDocumentInteraction)
    document.addEventListener('focusin', onDocumentInteraction)

    return () => {
      document.removeEventListener('pointerdown', onDocumentInteraction)
      document.removeEventListener('focusin', onDocumentInteraction)
    }
  }, [])

  function close() {
    setOpen(false)
    setTerm('')
    onNavigate?.()
  }

  function submit() {
    if (term.trim().length < MIN_QUERY_LENGTH) {
      return
    }

    navigate(`/search?q=${encodeURIComponent(term.trim())}`)
    close()
  }

  // Only once the debounce has caught up, so results for a half-typed word are never called empty.
  const settledAndQuiet = enabled && !isFetching && data?.query === settled

  return (
    <div ref={container} className="relative w-full max-w-xs">
      <label className="sr-only" htmlFor="site-search">
        Search matches, teams and series
      </label>

      <div className="relative">
        <Search
          aria-hidden="true"
          className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-subtle"
        />

        <input
          id="site-search"
          type="search"
          value={term}
          placeholder="Search"
          autoComplete="off"
          onChange={(event) => {
            setTerm(event.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              setOpen(false)
            }

            if (event.key === 'Enter') {
              event.preventDefault()
              submit()
            }
          }}
          className={cn(
            'h-10 w-full rounded-card border border-line bg-surface pl-9 pr-9 text-sm text-ink placeholder:text-ink-subtle',
            focusRing,
          )}
        />

        {term !== '' && (
          <button
            type="button"
            aria-label="Clear search"
            onClick={() => {
              setTerm('')
              setOpen(false)
            }}
            className={cn(
              'absolute right-2 top-1/2 inline-flex size-6 -translate-y-1/2 items-center justify-center rounded-full text-ink-subtle hover:bg-surface-muted',
              focusRing,
            )}
          >
            <X className="size-3.5" />
          </button>
        )}
      </div>

      {open && enabled && (
        /* `surface-raised` and `shadow-lift`: this covers the page rather than sitting on it, so
           it needs an opaque fill and a shadow the dark theme can actually show. */
        <div className="absolute right-0 z-20 mt-2 w-80 max-w-[calc(100vw-2rem)] overflow-hidden rounded-card border border-line bg-surface-raised p-2 shadow-lift backdrop-blur-xl">
          {/*
            Announced politely so a screen reader hears the count settle rather than every
            intermediate state as the term is typed.
          */}
          <p aria-live="polite" className="sr-only">
            {settledAndQuiet ? `${data.total} results for ${settled}` : 'Searching'}
          </p>

          {data === undefined ? (
            <p className="px-3 py-2 text-sm text-ink-subtle">Searching…</p>
          ) : data.total === 0 ? (
            <p className="px-3 py-2 text-sm text-ink-subtle">
              {settledAndQuiet ? `Nothing matched “${settled}”.` : 'Searching…'}
            </p>
          ) : (
            <>
              <SearchResultGroups results={data} onNavigate={close} />
              <button
                type="button"
                onClick={submit}
                className={cn(
                  'mt-1 block w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-brand-strong hover:bg-surface-muted',
                  focusRing,
                )}
              >
                See all results
              </button>
            </>
          )}
        </div>
      )}
    </div>
  )
}
