import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { SubjectsScreen } from './subjects-screen';

const MATHS = { id: 'maths', name: 'Mathematics', code: 'MTH', description: null, status: 'Active', mappedLevelCount: 9, armExceptionCount: 0, pupilsTakingCount: 240 };

function renderScreen() {
  return renderWithProviders(
    <MemoryRouter>
      <SubjectsScreen />
    </MemoryRouter>,
  );
}

describe('SubjectsScreen', () => {
  it('loading, then the list with each subject\'s class count', async () => {
    mockMe('subject.view');
    server.use(http.get(apiUrl('/api/v1/subjects'), () => HttpResponse.json({ items: [MATHS], nextCursor: null })));

    renderScreen();

    expect(screen.getByText('Loading subjects…')).toBeInTheDocument();
    expect(await screen.findByRole('cell', { name: 'Mathematics' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: '9' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New subject' })).not.toBeInTheDocument();
  });

  it('empty and error states', async () => {
    mockMe('subject.view');
    server.use(http.get(apiUrl('/api/v1/subjects'), () => HttpResponse.json({ items: [], nextCursor: null })));
    const { unmount } = renderScreen();
    expect(await screen.findByText('No subjects yet.')).toBeInTheDocument();
    unmount();

    server.use(http.get(apiUrl('/api/v1/subjects'), () => problemResponse(500)));
    renderScreen();
    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('creates a subject with an upper-cased code', async () => {
    mockMe('subject.view', 'subject.create');
    let body: unknown;
    server.use(
      http.get(apiUrl('/api/v1/subjects'), () => HttpResponse.json({ items: [], nextCursor: null })),
      http.post(apiUrl('/api/v1/subjects'), async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(MATHS, { status: 201 });
      }),
    );

    const { user } = renderScreen();
    await user.click(await screen.findByRole('button', { name: 'New subject' }));
    await user.type(screen.getByLabelText('Name'), 'Mathematics');
    await user.type(screen.getByLabelText('Short code (optional)'), 'mth');
    await user.click(screen.getByRole('button', { name: 'Create subject' }));

    await waitFor(() => expect(body).toEqual({ name: 'Mathematics', code: 'MTH', description: null }));
  });
});
