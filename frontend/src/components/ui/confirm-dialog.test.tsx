import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { ApiError } from '@/lib/http';
import { ConfirmDialog } from './confirm-dialog';

function renderDialog(onConfirm: () => Promise<unknown>, onClose = vi.fn()) {
  const user = userEvent.setup();
  render(
    <ConfirmDialog
      title="Delete Primary 1?"
      description="This cannot be undone."
      confirmLabel="Delete level"
      pendingLabel="Deleting…"
      onConfirm={onConfirm}
      onClose={onClose}
    />,
  );
  return { user, onClose };
}

describe('ConfirmDialog', () => {
  it('names what is at stake and closes once the action succeeds', async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    const { user, onClose } = renderDialog(onConfirm);

    expect(screen.getByRole('dialog', { name: 'Delete Primary 1?' })).toBeInTheDocument();
    expect(screen.getByText('This cannot be undone.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Delete level' }));

    expect(onConfirm).toHaveBeenCalledOnce();
    await waitFor(() => expect(onClose).toHaveBeenCalledOnce());
  });

  it('stays open and shows the server message when the action fails', async () => {
    const onConfirm = vi.fn().mockRejectedValue(new ApiError('Primary 1 is still in use.', { kind: 'conflict', status: 409 }));
    const { user, onClose } = renderDialog(onConfirm);

    await user.click(screen.getByRole('button', { name: 'Delete level' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Primary 1 is still in use.');
    expect(screen.getByRole('button', { name: 'Delete level' })).toBeEnabled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('cancel closes without acting', async () => {
    const onConfirm = vi.fn();
    const { user, onClose } = renderDialog(onConfirm);

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalledOnce();
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
