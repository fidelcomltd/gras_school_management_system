import { describe, expect, it } from 'vitest';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { pupil } from '../test-fixtures';
import { CreatePupilDialog } from './create-pupil-dialog';

const LEVELS = { items: [{ id: 'level-1', name: 'Primary 1', status: 'Active' }], nextCursor: null };

async function fillRequired(user: ReturnType<typeof renderWithProviders>['user']) {
  await user.type(screen.getByLabelText('Surname'), 'Okafor');
  await user.type(screen.getByLabelText('First name'), 'Chidera');
  await user.click(screen.getByRole('radio', { name: 'Female' }));
  await user.type(screen.getByLabelText('Date of birth'), '2020-05-03');
  await user.type(screen.getByLabelText('State of origin'), 'Imo');
  await user.type(screen.getByLabelText('LGA'), 'Owerri Municipal');
  await user.type(screen.getByLabelText('Home address'), '3 Wetheral Road, Owerri');
  await user.click(screen.getByRole('combobox', { name: 'Class admitted into' }));
  await user.click(await screen.findByRole('option', { name: 'Primary 1' }));
}

describe('CreatePupilDialog', () => {
  it('validates required fields before any request', async () => {
    let created = false;
    server.use(
      http.get(apiUrl('/api/v1/levels'), () => HttpResponse.json(LEVELS)),
      http.post(apiUrl('/api/v1/pupils'), () => {
        created = true;
        return HttpResponse.json(pupil(), { status: 201 });
      }),
    );

    const { user } = renderWithProviders(<CreatePupilDialog onClose={() => {}} onCreated={() => {}} />);
    await user.click(screen.getByRole('button', { name: 'Create pupil' }));

    expect(await screen.findByText('Surname is required.')).toBeInTheDocument();
    expect(created).toBe(false);
  });

  it('warns about a same-name, same-birthday pupil and creates only on the second click', async () => {
    let posts = 0;
    let sentLevel: string | undefined;
    server.use(
      http.get(apiUrl('/api/v1/levels'), () => HttpResponse.json(LEVELS)),
      http.get(apiUrl('/api/v1/pupils/duplicates'), () =>
        HttpResponse.json([pupil({ id: 'existing', registrationNumber: 'GRAS/2025/0007', status: 'Active' })]),
      ),
      http.post(apiUrl('/api/v1/pupils'), async ({ request }) => {
        posts += 1;
        sentLevel = ((await request.json()) as { admission: { classAdmittedInto: string } }).admission.classAdmittedInto;
        return HttpResponse.json(pupil(), { status: 201 });
      }),
    );
    let createdId: string | undefined;

    const { user } = renderWithProviders(<CreatePupilDialog onClose={() => {}} onCreated={(created) => (createdId = created.id)} />);
    await fillRequired(user);
    await user.click(screen.getByRole('button', { name: 'Create pupil' }));

    expect(await screen.findByText(/already exists/)).toBeInTheDocument();
    expect(screen.getByText(/GRAS\/2025\/0007/)).toBeInTheDocument();
    expect(posts).toBe(0);

    await user.click(screen.getByRole('button', { name: 'Create anyway' }));

    await waitFor(() => expect(createdId).toBe('pupil-1'));
    expect(posts).toBe(1);
    expect(sentLevel).toBe('level-1');
  }, 20_000); // types into nine fields: slow under a loaded full-suite run
});
