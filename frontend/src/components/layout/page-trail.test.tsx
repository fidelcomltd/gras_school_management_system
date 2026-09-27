import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@/test/render';
import { PageTrail } from './page-trail';

describe('PageTrail', () => {
  it('goes back to the parent page and shows the path from Home, marking the current page', () => {
    render(
      <MemoryRouter>
        <PageTrail trail={[{ label: 'Pupils', to: '/pupils' }, { label: 'OKAFOR Chidera' }]} />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'Back to Pupils' })).toHaveAttribute('href', '/pupils');
    const trail = screen.getByRole('navigation', { name: 'Breadcrumb' });
    expect(within(trail).getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/');
    expect(within(trail).getByRole('link', { name: 'Pupils' })).toHaveAttribute('href', '/pupils');
    expect(within(trail).getByText('OKAFOR Chidera')).toHaveAttribute('aria-current', 'page');
  });

  it('goes back Home when the page sits directly under it', () => {
    render(
      <MemoryRouter>
        <PageTrail trail={[{ label: 'Change password' }]} />
      </MemoryRouter>,
    );

    expect(screen.queryByRole('link', { name: /^Back to/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/');
  });
});
