import { Menu, X } from 'lucide-react'
import { useEffect } from 'react'
import { NavLink, useLocation } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { Container } from '@/components/layout/Container'
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
    'flex min-h-11 items-center rounded-card px-3 text-sm font-medium transition-colors sm:min-h-10',
    focusRing,
    isActive
      ? 'bg-brand-soft text-brand-strong'
      : 'text-ink-muted hover:bg-surface-muted hover:text-ink',
  )
}

export function Header() {
  const isMobileNavOpen = useUiStore((state) => state.isMobileNavOpen)
  const setMobileNavOpen = useUiStore((state) => state.setMobileNavOpen)
  const { pathname } = useLocation()

  useEffect(() => {
    setMobileNavOpen(false)
  }, [pathname, setMobileNavOpen])

  return (
    <header className="sticky top-0 z-10 border-b border-line bg-surface/95 backdrop-blur">
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

        {/* Pushed to the right of the nav on wide screens, and inside the menu on narrow ones. */}
        <div className="ml-auto hidden sm:block">
          <SearchBox />
        </div>

        <button
          type="button"
          onClick={() => setMobileNavOpen(!isMobileNavOpen)}
          aria-expanded={isMobileNavOpen}
          aria-controls="mobile-nav"
          aria-label={isMobileNavOpen ? 'Close menu' : 'Open menu'}
          className={cn(
            'inline-flex size-11 items-center justify-center rounded-card text-ink-muted hover:bg-surface-muted sm:hidden',
            focusRing,
          )}
        >
          {isMobileNavOpen ? <X className="size-5" /> : <Menu className="size-5" />}
        </button>
      </Container>

      {isMobileNavOpen && (
        <nav
          id="mobile-nav"
          aria-label="Mobile"
          className="animate-drop border-t border-line sm:hidden"
        >
          <Container className="flex flex-col gap-1 py-2">
            <div className="px-1 pb-1">
              <SearchBox onNavigate={() => setMobileNavOpen(false)} />
            </div>

            {navItems.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.end} className={navLinkClasses}>
                {item.label}
              </NavLink>
            ))}
          </Container>
        </nav>
      )}
    </header>
  )
}
