import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Merges Tailwind classes so a caller's `className` reliably wins over a
 * component's defaults. `clsx` handles conditionals; `twMerge` resolves
 * conflicts (`px-4` + `px-6` collapses to `px-6` rather than both landing).
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
