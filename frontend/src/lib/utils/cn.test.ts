import { describe, expect, it } from 'vitest';
import { cn } from './cn';

describe('cn', () => {
  it('joins class names', () => {
    expect(cn('a', 'b')).toBe('a b');
  });

  it('drops falsy values', () => {
    expect(cn('a', false, undefined, null, 'b')).toBe('a b');
  });

  it('lets a later Tailwind class win over an earlier conflicting one', () => {
    // This is the whole reason cn exists: a caller's className must beat the
    // component's default rather than fighting it on specificity.
    expect(cn('px-4', 'px-6')).toBe('px-6');
    expect(cn('bg-primary', 'bg-destructive')).toBe('bg-destructive');
  });

  it('keeps non-conflicting utilities from the same family', () => {
    expect(cn('px-4', 'py-2')).toBe('px-4 py-2');
  });

  it('handles conditional objects and arrays', () => {
    expect(cn(['a', { b: true, c: false }])).toBe('a b');
  });
});
