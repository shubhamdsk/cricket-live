import { create } from 'zustand'

interface UiState {
  isMobileNavOpen: boolean
  setMobileNavOpen: (isOpen: boolean) => void
}

export const useUiStore = create<UiState>((set) => ({
  isMobileNavOpen: false,
  setMobileNavOpen: (isMobileNavOpen) => set({ isMobileNavOpen }),
}))
