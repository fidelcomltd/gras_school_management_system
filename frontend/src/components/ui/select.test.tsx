import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, userEvent, waitFor } from '@/test/render';
import { Select, SelectContent, SelectItem, SelectTrigger } from './select';

const TERMS = [
  { value: 'michaelmas', label: 'Michaelmas' },
  { value: 'hilary', label: 'Hilary' },
  { value: 'trinity', label: 'Trinity' },
];

function Harness({
  onChange,
  withItems = true,
}: {
  onChange?: (value: string | null) => void;
  withItems?: boolean;
}) {
  const [value, setValue] = useState<string | null>(null);
  return (
    <Select
      {...(withItems ? { items: TERMS } : {})}
      value={value}
      onValueChange={(next) => {
        setValue(next);
        onChange?.(next);
      }}
    >
      <SelectTrigger aria-label="Term" placeholder="Choose a term" />
      <SelectContent>
        {TERMS.map((term) => (
          <SelectItem key={term.value} value={term.value}>
            {term.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

describe('Select', () => {
  it('shows the placeholder before anything is chosen', () => {
    render(<Harness />);
    expect(screen.getByRole('combobox', { name: 'Term' })).toHaveTextContent('Choose a term');
  });

  it('opens the listbox on click', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('combobox', { name: 'Term' }));

    expect(await screen.findByRole('listbox')).toBeInTheDocument();
    expect(await screen.findAllByRole('option')).toHaveLength(TERMS.length);
  });

  it('reports the chosen value and shows its label in the trigger', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<Harness onChange={onChange} />);

    await user.click(screen.getByRole('combobox', { name: 'Term' }));
    await user.click(await screen.findByRole('option', { name: 'Hilary' }));

    expect(onChange).toHaveBeenCalledWith('hilary');
    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Term' })).toHaveTextContent('Hilary'),
    );
  });

  it('falls back to the raw value in the trigger when items is omitted', async () => {
    // Pins the Base UI behaviour that makes `items` mandatory in practice: the
    // trigger cannot map a value to a label without it, so the user sees the id.
    // If a future version resolves labels without `items`, this test fails and
    // the warning in select.tsx can be relaxed.
    const user = userEvent.setup();
    render(<Harness withItems={false} />);

    await user.click(screen.getByRole('combobox', { name: 'Term' }));
    await user.click(await screen.findByRole('option', { name: 'Hilary' }));

    await waitFor(() =>
      expect(screen.getByRole('combobox', { name: 'Term' })).toHaveTextContent('hilary'),
    );
  });

  it('marks the chosen option as selected for assistive tech', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('combobox', { name: 'Term' }));
    await user.click(await screen.findByRole('option', { name: 'Trinity' }));
    await user.click(screen.getByRole('combobox', { name: 'Term' }));

    expect(await screen.findByRole('option', { name: 'Trinity' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });

  it('opens from the keyboard', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.tab();
    expect(screen.getByRole('combobox', { name: 'Term' })).toHaveFocus();

    await user.keyboard('{Enter}');
    expect(await screen.findByRole('listbox')).toBeInTheDocument();
  });

  it('closes on Escape without choosing anything', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<Harness onChange={onChange} />);

    await user.click(screen.getByRole('combobox', { name: 'Term' }));
    await screen.findByRole('listbox');
    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    expect(onChange).not.toHaveBeenCalled();
  });
});
