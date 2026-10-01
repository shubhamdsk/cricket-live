import { Moon, Sun } from 'lucide-react'

import { focusRing } from '@/components/common/focusRing'
import { useThemeStore } from '@/store/themeStore'
import { cn } from '@/utils/cn'

/**
 * The icon shows the destination, not the current state: a sun while dark is on, because pressing
 * it turns the lights on. Showing the current state instead is the commoner mistake and leaves
 * people clicking a moon to get a moon.
 *
 * Not a switch or a checkbox. `aria-pressed` would describe this as a thing that is on or off, and
 * neither theme is the "off" one, so the label carries the meaning instead and changes with the
 * theme. A screen reader therefore reads the action rather than a state it has to interpret.
 */
export function ThemeToggle() {
  const theme = useThemeStore((state) => state.theme)
  const toggleTheme = useThemeStore((state) => state.toggleTheme)

  const isDark = theme === 'dark'

  return (
    <button
      type="button"
      onClick={toggleTheme}
      title={isDark ? 'Switch to light theme' : 'Switch to dark theme'}
      aria-label={isDark ? 'Switch to light theme' : 'Switch to dark theme'}
      className={cn(
        'inline-flex size-11 items-center justify-center rounded-card text-ink-muted transition-colors hover:bg-surface-muted hover:text-ink sm:size-10',
        focusRing,
      )}
    >
      {isDark ? <Sun className="size-5" /> : <Moon className="size-5" />}
    </button>
  )
}
