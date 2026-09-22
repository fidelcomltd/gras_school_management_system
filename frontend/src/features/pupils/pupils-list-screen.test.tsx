import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { PupilsListScreen } from './pupils-list-screen';
import { mockMe, pupil } from './test-fixtures';

function renderScreen() {
  return renderWithProviders(
    <MemoryRouter>
      <PupilsListScreen />
    </MemoryRouter>,
  );
}

describe('PupilsListScreen — four required states', () => {
  it('loading: shows a status before /pupils resolves', () => {
    mockMe('pupil.view');
    server.use(http.get(apiUrl('/api/v1/pupils'), () => new Promise(() => undefined)));

    renderScreen();

    expect(screen.getByText('Loading pupils…')).toBeInTheDocument();
  });

  it('empty: says so rather than showing a blank list', async () => {
    mockMe('pupil.view');
    server.use(http.get(apiUrl('/api/v1/pupils'), () => HttpResponse.json({ items: [], nextCursor: null })));

    renderScreen();

    expect(await screen.findByText('No pupils yet.')).toBeInTheDocument();
  });

  it('error: a server failure offers a retry', async () => {
    mockMe('pupil.view');
    server.use(http.get(apiUrl('/api/v1/pupils'), () => problemResponse(500)));

    renderScreen();

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('unauthorized: renders no list', async () => {
    mockMe('pupil.view');
    server.use(http.get(apiUrl('/api/v1/pupils'), () => problemResponse(401, { errorCode: 'authentication.required' })));

    renderScreen();

    await waitFor(() => expect(screen.queryByText('Loading pupils…')).not.toBeInTheDocument());
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});

describe('PupilsListScreen — search and create', () => {
  it('lists pupils surname-first and sends the search term to the server', async () => {
    mockMe('pupil.view');
    const searches: (string | null)[] = [];
    server.use(
      http.get(apiUrl('/api/v1/pupils'), ({ request }) => {
        searches.push(new URL(request.url).searchParams.get('search'));
        return HttpResponse.json({ items: [pupil({ registrationNumber: 'GRAS/2026/0041', status: 'Active' })], nextCursor: null });
      }),
    );

    const { user } = renderScreen();

    expect(await screen.findByRole('link', { name: /OKAFOR Chidera/ })).toHaveTextContent('GRAS/2026/0041 · Active');
    await user.type(screen.getByLabelText('Search by name or registration number'), 'okafor');
    await user.click(screen.getByRole('button', { name: 'Search' }));

    await waitFor(() => expect(searches).toContain('okafor'));
  });

  it('shows New pupil only with pupil.create', async () => {
    mockMe('pupil.view');
    server.use(http.get(apiUrl('/api/v1/pupils'), () => HttpResponse.json({ items: [], nextCursor: null })));

    renderScreen();

    await screen.findByText('No pupils yet.');
    expect(screen.queryByRole('button', { name: 'New pupil' })).not.toBeInTheDocument();
  });
});
