import { describe, expect, it, onTestFinished, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FormError } from './query-states';

describe('FormError', () => {
  it('scrolls into view when a message arrives and stays pinned while the user scrolls', () => {
    const original = window.HTMLElement.prototype.scrollIntoView;
    const scrollIntoView = vi.fn();
    window.HTMLElement.prototype.scrollIntoView = scrollIntoView;
    onTestFinished(() => {
      window.HTMLElement.prototype.scrollIntoView = original;
    });

    const { rerender } = render(<FormError message={null} />);
    expect(scrollIntoView).not.toHaveBeenCalled();

    rerender(<FormError message="That email is already in use." />);

    expect(screen.getByRole('alert')).toHaveTextContent('That email is already in use.');
    expect(screen.getByRole('alert')).toHaveClass('in-[form]:sticky');
    expect(scrollIntoView).toHaveBeenCalledTimes(1);
  });
});
