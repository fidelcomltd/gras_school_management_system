import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent } from '@/test/render';
import { Button } from './button';

describe('Button', () => {
  it('renders an accessible button with its label', () => {
    render(<Button>Enrol student</Button>);
    expect(screen.getByRole('button', { name: 'Enrol student' })).toBeInTheDocument();
  });

  it('defaults to type="button" so it cannot accidentally submit a form', () => {
    render(<Button>Save</Button>);
    expect(screen.getByRole('button')).toHaveAttribute('type', 'button');
  });

  it('still allows an explicit submit type', () => {
    render(<Button type="submit">Save</Button>);
    expect(screen.getByRole('button')).toHaveAttribute('type', 'submit');
  });

  it('calls onClick', async () => {
    const onClick = vi.fn();
    const user = userEvent.setup();
    render(<Button onClick={onClick}>Go</Button>);

    await user.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('does not fire when disabled', async () => {
    const onClick = vi.fn();
    const user = userEvent.setup();
    render(
      <Button disabled onClick={onClick}>
        Go
      </Button>,
    );

    await user.click(screen.getByRole('button'));
    expect(onClick).not.toHaveBeenCalled();
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('applies variant and size classes from the token layer', () => {
    render(
      <Button variant="destructive" size="lg">
        Delete
      </Button>,
    );
    const button = screen.getByRole('button');
    expect(button.className).toContain('bg-destructive');
    expect(button.className).toContain('h-11');
  });

  it('lets a caller className override a conflicting default', () => {
    render(<Button className="h-20">Tall</Button>);
    const className = screen.getByRole('button').className;
    expect(className).toContain('h-20');
    expect(className).not.toContain('h-10');
  });

  it('composes into another element via render, keeping its styling', () => {
    // Base UI's `render` is the asChild equivalent.
    render(<Button render={<a href="/students">Students</a>} />);

    const link = screen.getByRole('link', { name: 'Students' });
    expect(link).toHaveAttribute('href', '/students');
    expect(link.className).toContain('bg-primary');
  });

  it('is reachable and activatable by keyboard', async () => {
    const onClick = vi.fn();
    const user = userEvent.setup();
    render(<Button onClick={onClick}>Go</Button>);

    await user.tab();
    expect(screen.getByRole('button')).toHaveFocus();

    await user.keyboard('{Enter}');
    expect(onClick).toHaveBeenCalled();
  });
});
