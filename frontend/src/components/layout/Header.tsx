import { Search, X } from 'lucide-react'
import { useEffect } from 'react'
import { NavLink, useLocation } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { Container } from '@/components/layout/Container'
import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { SearchBox } from '@/features/search/components/SearchBox'
import { useUiStore } from '@/store/uiStore'
import { cn } from '@/utils/cn'

const navItems = [
  { to: '/', label: 'Home', end: true },
  { to: '/live', label: 'Live', end: false },
  { to: '/matches', label: 'Matches', end: false },
  { to: '/series', label: 'Series', end: false },
  { to: '/teams', label: 'Teams', end: false },
]

function navLinkClasses({ isActive }: { isActive: boolean }) {
  return cn(
    'flex min-h-11 shrink-0 items-center whitespace-nowrap rounded-card px-3 text-sm font-medium transition-colors sm:min-h-10',
    focusRing,
    isActive
      ? 'bg-brand-soft text-brand-strong'
      : 'text-ink-muted hover:bg-surface-muted hover:text-ink',
  )
}

export function Header() {
  const isMobileSearchOpen = useUiStore((state) => state.isMobileSearchOpen)
  const setMobileSearchOpen = useUiStore((state) => state.setMobileSearchOpen)
  const { pathname } = useLocation()

  useEffect(() => {
    setMobileSearchOpen(false)
  }, [pathname, setMobileSearchOpen])

  return (
    /* `bg-chrome` rather than a translucent panel colour: content scrolls under this, and blurring
       it through white would turn the page milky instead of navy. */
    <header className="glass sticky top-0 z-10 border-b border-line bg-chrome">
      <Container className="flex items-center justify-between gap-4 py-3">
        <NavLink
          to="/"
          className={cn('rounded-lg text-lg font-semibold tracking-tight', focusRing)}
        >
          Cricket<span className="text-brand-strong">Live</span>
        </NavLink>

        <nav className="hidden items-center gap-1 sm:flex" aria-label="Main">
          {navItems.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={navLinkClasses}>
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-1">
          <div className="hidden sm:block">
            <SearchBox />
          </div>

          <ThemeToggle />

          <button
            type="button"
            onClick={() => setMobileSearchOpen(!isMobileSearchOpen)}
            aria-expanded={isMobileSearchOpen}
            aria-controls="mobile-search"
            aria-label={isMobileSearchOpen ? 'Close search' : 'Search'}
            className={cn(
              'inline-flex size-11 items-center justify-center rounded-card text-ink-muted hover:bg-surface-muted sm:hidden',
              focusRing,
            )}
          >
            {isMobileSearchOpen ? <X className="size-5" /> : <Search className="size-5" />}
          </button>
        </div>
      </Container>

      {isMobileSearchOpen && (
        <div id="mobile-search" className="animate-drop border-t border-line sm:hidden">
          <Container className="py-2">
            <SearchBox onNavigate={() => setMobileSearchOpen(false)} />
          </Container>
        </div>
      )}

      {/*
        Always on screen rather than behind a menu button, so every section is one tap away. It
        scrolls sideways once there are more sections than fit across a phone.
      */}
      <nav aria-label="Main" className="border-t border-line sm:hidden">
        <Container className="flex gap-1 overflow-x-auto py-1.5">
          {navItems.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={navLinkClasses}>
              {item.label}
            </NavLink>
          ))}
        </Container>
      </nav>
    </header>
  )
}
