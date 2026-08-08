import { describe, expect, it } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { Button } from './button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from './dialog';

function Harness({ showCloseButton = true }: { showCloseButton?: boolean }) {
  return (
    <Dialog>
      <DialogTrigger render={<Button>Open</Button>} />
      <DialogContent showCloseButton={showCloseButton}>
        <DialogHeader>
          <DialogTitle>Withdraw student</DialogTitle>
          <DialogDescription>This removes them from the current roll.</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <DialogClose render={<Button variant="ghost">Cancel</Button>} />
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

describe('Dialog', () => {
  it('is closed until the trigger is used', () => {
    render(<Harness />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('opens from the trigger', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: 'Open' }));

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
  });

  it('labels itself from the title and description', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Open' }));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveAccessibleName('Withdraw student');
    expect(dialog).toHaveAccessibleDescription('This removes them from the current roll.');
  });

  it('closes on Escape', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Open' }));
    await screen.findByRole('dialog');

    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('closes from a DialogClose child', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Open' }));
    await screen.findByRole('dialog');

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('exposes a labelled dismiss button by default', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Open' }));
    await screen.findByRole('dialog');

    expect(screen.getByRole('button', { name: /close dialog/i })).toBeInTheDocument();
  });

  it('omits the dismiss button when asked', async () => {
    const user = userEvent.setup();
    render(<Harness showCloseButton={false} />);
    await user.click(screen.getByRole('button', { name: 'Open' }));
    await screen.findByRole('dialog');

    expect(screen.queryByRole('button', { name: /close dialog/i })).not.toBeInTheDocument();
  });

  it('moves focus into the dialog when it opens', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: 'Open' }));

    const dialog = await screen.findByRole('dialog');
    await waitFor(() => expect(dialog.contains(document.activeElement)).toBe(true));
  });
});
