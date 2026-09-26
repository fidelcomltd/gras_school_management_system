import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface SidebarState {
  /** The desktop sidebar shows icons only. The mobile drawer is always full width. */
  collapsed: boolean;
  toggle: () => void;
}

/** Remembered per browser, like the theme: a per-viewer convenience, never shared state. */
export const useSidebarStore = create<SidebarState>()(
  persist(
    (set, get) => ({
      collapsed: false,
      toggle: () => set({ collapsed: !get().collapsed }),
    }),
    { name: 'gra.sidebar', partialize: (state) => ({ collapsed: state.collapsed }) },
  ),
);
