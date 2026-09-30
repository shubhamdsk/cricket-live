import { create } from 'zustand'

interface UiState {
  isMobileNavOpen: boolean
  setMobileNavOpen: (isOpen: boolean) => void

  /**
   * What the route announcer should read out next, or the empty string for nothing pending.
   *
   * In the store rather than passed down because the thing that knows a page's name is the page,
   * and the thing that announces it is the layout above every page. Threading it through would
   * mean every route rendering an announcer of its own.
   */
  announcement: string
  announce: (announcement: string) => void
}

export const useUiStore = create<UiState>((set) => ({
  isMobileNavOpen: false,
  setMobileNavOpen: (isMobileNavOpen) => set({ isMobileNavOpen }),

  announcement: '',
  announce: (announcement) => set({ announcement }),
}))
