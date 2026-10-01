import { create } from 'zustand'

export type Theme = 'dark' | 'light'

/**
 * Shared with the pre-paint script in `index.html`, which cannot import anything. If either string
 * changes, both change together — a mismatch is silent and shows up only as a flash of the wrong
 * theme on the next hard refresh.
 */
const STORAGE_KEY = 'cricket-live:theme'
const LIGHT_CLASS = 'theme-light'

/** Kept in step with `--color-chrome`, since this is the strip of browser UI next to the header. */
const CHROME_COLOUR: Record<Theme, string> = {
  dark: '#060a16',
  light: '#ffffff',
}

interface ThemeState {
  theme: Theme
  setTheme: (theme: Theme) => void
  toggleTheme: () => void
}

/**
 * Reads the theme off the document rather than out of storage.
 *
 * The class is already correct by the time any of this runs — `index.html` sets it before the first
 * paint — so the document is the single source of truth and storage is only how it survives a
 * reload. Reading storage again here would introduce a second answer that could disagree with what
 * is on screen.
 */
function current(): Theme {
  return document.documentElement.classList.contains(LIGHT_CLASS) ? 'light' : 'dark'
}

function apply(theme: Theme): void {
  document.documentElement.classList.toggle(LIGHT_CLASS, theme === 'light')
  document.head
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute('content', CHROME_COLOUR[theme])

  try {
    localStorage.setItem(STORAGE_KEY, theme)
  } catch {
    // Private browsing and a few locked-down configurations refuse storage. Losing the preference
    // on reload is a smaller problem than the whole app failing to switch theme, so this is
    // swallowed rather than surfaced.
  }
}

export const useThemeStore = create<ThemeState>((set, get) => ({
  theme: current(),

  setTheme: (theme) => {
    apply(theme)
    set({ theme })
  },

  toggleTheme: () => get().setTheme(get().theme === 'dark' ? 'light' : 'dark'),
}))
