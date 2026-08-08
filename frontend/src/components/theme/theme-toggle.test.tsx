import { beforeEach, describe, expect, it } from 'vitest';
import { render, screen, userEvent } from '@/test/render';
import { useThemeStore } from '@/stores/theme-store';
import { ThemeToggle } from './theme-toggle';

beforeEach(() => {
  useThemeStore.setState({ preference: 'light', resolved: 'light' });
  document.documentElement.classList.remove('dark');
});

describe('ThemeToggle', () => {
  it('names the theme it will switch to, not the current one', () => {
    render(<ThemeToggle />);
    expect(screen.getByRole('button', { name: /switch to dark theme/i })).toBeInTheDocument();
  });

  it('reflects the current theme through aria-pressed', () => {
    render(<ThemeToggle />);
    expect(screen.getByRole('button')).toHaveAttribute('aria-pressed', 'false');
  });

  it('switches the theme and updates its own label', async () => {
    const user = userEvent.setup();
    render(<ThemeToggle />);

    await user.click(screen.getByRole('button'));

    expect(useThemeStore.getState().resolved).toBe('dark');
    expect(document.documentElement).toHaveClass('dark');
    expect(screen.getByRole('button', { name: /switch to light theme/i })).toBeInTheDocument();
    expect(screen.getByRole('button')).toHaveAttribute('aria-pressed', 'true');
  });

  it('switches back on a second press', async () => {
    const user = userEvent.setup();
    render(<ThemeToggle />);

    await user.click(screen.getByRole('button'));
    await user.click(screen.getByRole('button'));

    expect(useThemeStore.getState().resolved).toBe('light');
    expect(document.documentElement).not.toHaveClass('dark');
  });
});
