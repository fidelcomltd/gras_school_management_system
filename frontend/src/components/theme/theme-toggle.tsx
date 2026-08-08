import { Button } from '@/components/ui/button';
import { MoonIcon, SunIcon } from '@/components/ui/icons';
import { useThemeStore } from '@/stores/theme-store';

/**
 * Light/dark switch. `aria-pressed` plus a label that names the destination
 * ("Switch to dark theme") is what makes this usable without sight of the icon.
 */
export function ThemeToggle({ className }: { className?: string }) {
  const resolved = useThemeStore((state) => state.resolved);
  const toggle = useThemeStore((state) => state.toggle);
  const goingToDark = resolved === 'light';

  return (
    <Button
      variant="ghost"
      size="icon"
      className={className}
      onClick={toggle}
      aria-pressed={resolved === 'dark'}
      aria-label={`Switch to ${goingToDark ? 'dark' : 'light'} theme`}
      title={`Switch to ${goingToDark ? 'dark' : 'light'} theme`}
    >
      {goingToDark ? <MoonIcon /> : <SunIcon />}
    </Button>
  );
}
