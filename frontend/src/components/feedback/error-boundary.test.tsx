import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent } from '@/test/render';
import { ErrorBoundary } from './error-boundary';

function Explode({ shouldThrow }: { shouldThrow: boolean }) {
  if (shouldThrow) throw new Error('kaboom');
  return <p>Recovered content</p>;
}

beforeEach(() => {
  // React logs caught errors to the console; that noise is expected here.
  vi.spyOn(console, 'error').mockImplementation(() => undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('ErrorBoundary', () => {
  it('renders children when nothing throws', () => {
    render(
      <ErrorBoundary>
        <p>All good</p>
      </ErrorBoundary>,
    );
    expect(screen.getByText('All good')).toBeInTheDocument();
  });

  it('shows an alert instead of blanking when a child throws', () => {
    render(
      <ErrorBoundary>
        <Explode shouldThrow />
      </ErrorBoundary>,
    );

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText(/something went wrong/i)).toBeInTheDocument();
  });

  it('reports the error to onError', () => {
    const onError = vi.fn();
    render(
      <ErrorBoundary onError={onError}>
        <Explode shouldThrow />
      </ErrorBoundary>,
    );

    expect(onError).toHaveBeenCalledWith(
      expect.objectContaining({ message: 'kaboom' }),
      expect.anything(),
    );
  });

  it('renders a custom fallback when given one', () => {
    render(
      <ErrorBoundary fallback={(error) => <p>Custom: {error.message}</p>}>
        <Explode shouldThrow />
      </ErrorBoundary>,
    );

    expect(screen.getByText('Custom: kaboom')).toBeInTheDocument();
  });

  it('recovers when the retry button is pressed and the cause is gone', async () => {
    const user = userEvent.setup();

    function Harness() {
      return (
        <ErrorBoundary>
          <Explode shouldThrow={false} />
        </ErrorBoundary>
      );
    }

    const { rerender } = render(
      <ErrorBoundary>
        <Explode shouldThrow />
      </ErrorBoundary>,
    );

    await user.click(screen.getByRole('button', { name: /try again/i }));
    rerender(<Harness />);

    expect(screen.getByText('Recovered content')).toBeInTheDocument();
  });
});
