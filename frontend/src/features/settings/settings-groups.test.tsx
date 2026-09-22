import { beforeEach, describe, expect, it, vi } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor, within } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { SettingsScreen } from './settings-screen';

// GET /settings and /settings/result-rules answer with the contract's own examples (default handlers).
beforeEach(() => {
  Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:image'), revokeObjectURL: vi.fn() });
});

describe('SettingsScreen groups', () => {
  it('saves the grading scale whole, with the version it was read at', async () => {
    mockMe('settings.view', 'settings.grading.update');
    let body: { bands: { gradeLetter: string; lowerBound: number }[]; expectedVersion: number } | undefined;
    server.use(
      http.put(apiUrl('/api/v1/settings/grading'), async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json({});
      }),
    );

    const { user } = renderWithProviders(<SettingsScreen />);
    await user.click(await screen.findByRole('tab', { name: 'Grading' }));
    const letter = screen.getByLabelText('Band 1 grade');
    await user.clear(letter);
    await user.type(letter, 'A*');
    await user.click(screen.getByRole('button', { name: 'Save grading scale' }));

    await waitFor(() => expect(body?.bands[0]).toMatchObject({ gradeLetter: 'A*', lowerBound: 90 }));
    expect(body?.bands).toHaveLength(9);
    expect(body?.expectedVersion).toBe(0);
  });

  it('shows a locked assessment refusal verbatim', async () => {
    mockMe('settings.view', 'settings.assessment.update');
    server.use(
      http.put(apiUrl('/api/v1/settings/assessment'), () =>
        problemResponse(409, { detail: 'Marks have been entered this session, so the assessment structure is locked.' }),
      ),
    );

    const { user } = renderWithProviders(<SettingsScreen />);
    await user.click(await screen.findByRole('tab', { name: 'Assessment' }));
    expect(screen.getByText(/Maximum marks add up to/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Save assessment structure' }));

    expect(await screen.findByText('Marks have been entered this session, so the assessment structure is locked.')).toBeInTheDocument();
  });

  it('result rules: weights appear only for the weighted method and are sent only then', async () => {
    mockMe('settings.view', 'settings.resultrules.update', 'subject.view');
    let body: { annualMethod: string; weightFirst: number | null } | undefined;
    server.use(
      http.put(apiUrl('/api/v1/settings/result-rules'), async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json({});
      }),
    );

    const { user } = renderWithProviders(<SettingsScreen />);
    await user.click(await screen.findByRole('tab', { name: 'Result rules' }));
    expect(await screen.findByLabelText('Method')).toBeInTheDocument();
    expect(screen.queryByLabelText('First term %')).not.toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Method'), 'Weighted');
    expect(screen.getByLabelText('First term %')).toHaveValue('20');
    await user.click(screen.getByRole('button', { name: 'Save result rules' }));

    await waitFor(() => expect(body).toMatchObject({ annualMethod: 'Weighted', weightFirst: 20 }));
  });

  it('rating scales: an added point is appended in order and existing ids are kept', async () => {
    mockMe('settings.view', 'settings.ratingscales.update', 'level.view');
    let body: { scales: { id: string | null; points: { id: string | null; pointCode: string; pointOrder: number }[] }[] } | undefined;
    server.use(
      http.put(apiUrl('/api/v1/settings/rating-scales'), async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json({});
      }),
    );

    const { user } = renderWithProviders(<SettingsScreen />);
    await user.click(await screen.findByRole('tab', { name: 'Ratings' }));
    const scaleSection = screen.getByRole('region', { name: 'Rating scales' });
    await user.click(within(scaleSection).getAllByRole('button', { name: 'Add point' })[0] as HTMLElement);
    const codes = within(scaleSection).getAllByLabelText(/point \d+ code/);
    await user.type(codes[codes.length - 1] as HTMLElement, 'x');
    await user.click(screen.getByRole('button', { name: 'Save rating scales' }));

    await waitFor(() => expect(body).toBeDefined());
    const points = body?.scales[0]?.points ?? [];
    expect(points[0]?.id).not.toBeNull();
    expect(points[points.length - 1]).toEqual({ id: null, pointCode: 'X', pointLabel: '', pointOrder: points.length });
  });

  it('traits: a new psychomotor trait is numbered within its own block', async () => {
    mockMe('settings.view', 'settings.traits.update', 'level.view');
    let body: { traits: { id: string | null; domain: string; name: string; displayOrder: number }[] } | undefined;
    server.use(
      http.put(apiUrl('/api/v1/settings/traits'), async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json({});
      }),
    );

    const { user } = renderWithProviders(<SettingsScreen />);
    await user.click(await screen.findByRole('tab', { name: 'Ratings' }));
    const traits = screen.getByRole('region', { name: 'Traits' });
    const psychomotor = within(traits).getByRole('group', { name: 'Psychomotor skills' });
    await user.click(within(psychomotor).getByRole('button', { name: 'Add trait' }));
    await user.type(within(psychomotor).getAllByLabelText('Psychomotor skills trait name').at(-1) as HTMLElement, 'Handwriting');
    await user.click(screen.getByRole('button', { name: 'Save traits' }));

    await waitFor(() => expect(body?.traits.find((t) => t.name === 'Handwriting')).toEqual({ id: null, domain: 'Psychomotor', name: 'Handwriting', displayOrder: 1, status: 'Active' }));
  });

  it('read-only without the group privilege', async () => {
    mockMe('settings.view');

    const { user } = renderWithProviders(<SettingsScreen />);
    await user.click(await screen.findByRole('tab', { name: 'Grading' }));

    expect(screen.getByLabelText('Band 1 grade')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Save grading scale' })).not.toBeInTheDocument();
  });
});
