import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor, within } from '@/test/render';
import { PageTrail } from './page-trail';

describe('PageTrail', () => {
  it('goes back to the parent page and shows the path from Home, marking the current page', () => {
    renderWithProviders(
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
    renderWithProviders(
      <MemoryRouter>
        <PageTrail trail={[{ label: 'Change password' }]} />
      </MemoryRouter>,
    );

    expect(screen.queryByRole('link', { name: /^Back to/ })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute('href', '/');
  });

  it("takes a menu page's label from the sidebar, and never links a page the caller cannot open", async () => {
    mockMe('report.view'); // not weekly.view, which the Weekly reports page needs
    renderWithProviders(
      <MemoryRouter>
        <PageTrail trail={[{ to: '/weekly' }, { label: 'Weekly report completion' }]} />
      </MemoryRouter>,
    );

    // The session loads first (in the app it is always cached by then); the trail follows it.
    await waitFor(() => expect(screen.queryByRole('link', { name: 'Back to Weekly reports' })).not.toBeInTheDocument());
    const trail = screen.getByRole('navigation', { name: 'Breadcrumb' });
    expect(within(trail).getByText('Weekly reports')).not.toHaveAttribute('href');
  });

  it('mutes an unlinked middle crumb, so only the last reads as the current page', () => {
    renderWithProviders(
      <MemoryRouter>
        <PageTrail trail={[{ label: 'Pupils', to: '/pupils' }, { label: 'Term 1' }, { label: 'Primary 2' }]} />
      </MemoryRouter>,
    );

    expect(screen.getByText('Term 1')).not.toHaveAttribute('aria-current');
    expect(screen.getByText('Primary 2')).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Back to Pupils' })).toHaveAttribute('href', '/pupils');
  });
});
