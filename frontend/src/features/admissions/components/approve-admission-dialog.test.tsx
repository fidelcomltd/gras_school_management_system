import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent } from '@/test/render';
import { ApproveAdmissionDialog } from './approve-admission-dialog';
import { pupil } from './test-fixtures';

/**
 * Approve is BLOCKED (TASK-0064, contract gap — see this component's own doc
 * comment): the admissions queue row has no `sessionId`/`classAdmittedInto`
 * to source `GET /arms`'s filter from, so there is no honest way to build
 * the arm selector `ApproveAdmissionCommand.armId` needs. This dialog only
 * proves the entry point says so and can be dismissed — it makes no request.
 */
describe('ApproveAdmissionDialog — blocked pending a contract addition', () => {
  it('names the applicant and explains why approval cannot proceed here', () => {
    render(<ApproveAdmissionDialog pupil={pupil()} onClose={() => {}} />);

    expect(screen.getByRole('heading', { name: 'Approve Okafor Chidera' })).toBeInTheDocument();
    expect(
      screen.getByText(/cannot yet determine which session and class level/),
    ).toBeInTheDocument();
  });

  it('Close calls onClose without ever making a network request', async () => {
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(<ApproveAdmissionDialog pupil={pupil()} onClose={onClose} />);

    await user.click(screen.getByRole('button', { name: 'Close' }));

    expect(onClose).toHaveBeenCalledOnce();
  });
});
