import { describe, expect, it } from 'vitest';
import { mockMe } from '@/test/mock-me';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { apiUrl, http, HttpResponse, problemResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import { AuditScreen } from './audit-screen';

const EVENT = {
  id: '48213',
  occurredAtUtc: '2026-08-03T09:30:00+00:00',
  actorAdminId: 'a-1',
  actorLabel: 'Chisom Maxwell <chisom@example.com>',
  action: 'result.publish',
  entityType: 'result_set',
  entityId: 'rs-1',
  outcome: 'Rejected',
  beforeJson: '{"state":"Approved"}',
  afterJson: null,
  reason: 'Head teacher remark missing',
  sourceIp: '197.210.64.0/24',
  userAgent: 'Mozilla/5.0',
};

describe('AuditScreen', () => {
  it('lists events in Lagos time with their outcome, and filters by action and dates', async () => {
    mockMe('audit.view');
    const queries: URLSearchParams[] = [];
    server.use(
      http.get(apiUrl('/api/v1/audit-events'), ({ request }) => {
        queries.push(new URL(request.url).searchParams);
        return HttpResponse.json({ items: [EVENT], nextCursor: null });
      }),
    );

    const { user } = renderWithProviders(<AuditScreen />);

    expect(await screen.findByText('result.publish')).toBeInTheDocument();
    expect(screen.getByText('refused')).toBeInTheDocument();
    expect(screen.getByText('03/08/2026, 10:30:00')).toBeInTheDocument();
    expect(screen.getByText('Reason: Head teacher remark missing')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Export CSV' })).not.toBeInTheDocument();

    await user.type(screen.getByLabelText('Action, e.g. result.publish'), 'result.publish');
    await user.type(screen.getByLabelText('From'), '2026-08-01');
    await user.click(screen.getByRole('button', { name: 'Filter' }));

    await waitFor(() => expect(queries.at(-1)?.get('action')).toBe('result.publish'));
    expect(queries.at(-1)?.get('fromUtc')).toBe('2026-07-31T23:00:00.000Z');
  });

  it('empty and error states', async () => {
    mockMe('audit.view');
    server.use(http.get(apiUrl('/api/v1/audit-events'), () => HttpResponse.json({ items: [], nextCursor: null })));
    const { unmount } = renderWithProviders(<AuditScreen />);
    expect(await screen.findByText('No events match these filters.')).toBeInTheDocument();
    unmount();

    server.use(http.get(apiUrl('/api/v1/audit-events'), () => problemResponse(500)));
    renderWithProviders(<AuditScreen />);
    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });
});
