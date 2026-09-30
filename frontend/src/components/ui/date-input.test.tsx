import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { describe, expect, it, vi } from 'vitest';
import { renderWithProviders, screen, waitFor } from '@/test/render';
import { DateInput } from './date-input';

function Controlled({ onValue }: { onValue: (value: string) => void }) {
  const [value, setValue] = useState('2026-09-01');
  return (
    <>
      <DateInput
        aria-label="Day"
        value={value}
        onChange={(event) => {
          setValue(event.target.value);
          onValue(event.target.value);
        }}
      />
      <button type="button" onClick={() => setValue('')}>
        Clear
      </button>
    </>
  );
}

function Registered({ onSubmit }: { onSubmit: (values: { day: string }) => void }) {
  const { register, handleSubmit, reset } = useForm({ defaultValues: { day: '2026-01-15' } });
  return (
    <form onSubmit={handleSubmit(onSubmit)}>
      <DateInput aria-label="Day" {...register('day')} />
      <button type="button" onClick={() => reset({ day: '2027-12-31' })}>
        Reset
      </button>
      <button type="submit">Submit</button>
    </form>
  );
}

describe('DateInput', () => {
  it('shows DD/MM/YYYY, inserts the slashes while typing, and reports ISO', async () => {
    const onValue = vi.fn();
    const { user } = renderWithProviders(<Controlled onValue={onValue} />);
    const input = screen.getByLabelText('Day');
    expect(input).toHaveValue('01/09/2026');

    await user.clear(input);
    await user.type(input, '03052020');
    expect(input).toHaveValue('03/05/2020');
    expect(onValue).toHaveBeenLastCalledWith('2020-05-03');
  });

  it('reports nothing for an impossible date and flags it once the field is left', async () => {
    const onValue = vi.fn();
    const { user } = renderWithProviders(<Controlled onValue={onValue} />);
    const input = screen.getByLabelText('Day');

    await user.clear(input);
    await user.type(input, '31/02/2026');
    expect(onValue).toHaveBeenLastCalledWith('');
    expect(input).not.toHaveAttribute('aria-invalid');
    await user.tab();
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText('Enter a real date as dd/mm/yyyy, for example 01/09/2026.')).toBeInTheDocument();
  });

  it('reports the same date again after the parent clears it', async () => {
    const onValue = vi.fn();
    const { user } = renderWithProviders(<Controlled onValue={onValue} />);
    const input = screen.getByLabelText('Day');

    await user.click(screen.getByRole('button', { name: 'Clear' }));
    expect(input).toHaveValue('');
    await user.type(input, '01092026');
    expect(onValue).toHaveBeenLastCalledWith('2026-09-01');
  });

  it('hands a registered field the badly typed text, so its schema can refuse it instead of saving nothing', async () => {
    const onSubmit = vi.fn();
    const { user } = renderWithProviders(<Registered onSubmit={onSubmit} />);
    const input = screen.getByLabelText('Day');

    await user.clear(input);
    await user.type(input, '31/02/2026');
    await user.click(screen.getByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(onSubmit.mock.calls[0]?.[0]).toEqual({ day: '31/02/2026' }));
  });

  it('works under react-hook-form register: defaults, reset and submit all in ISO', async () => {
    const onSubmit = vi.fn();
    const { user } = renderWithProviders(<Registered onSubmit={onSubmit} />);
    const input = screen.getByLabelText('Day');
    expect(input).toHaveValue('15/01/2026');

    await user.click(screen.getByRole('button', { name: 'Reset' }));
    expect(input).toHaveValue('31/12/2027');

    await user.clear(input);
    await user.type(input, '1/9/2026');
    await user.click(screen.getByRole('button', { name: 'Submit' }));
    await waitFor(() => expect(onSubmit.mock.calls[0]?.[0]).toEqual({ day: '2026-09-01' }));
    expect(input).toHaveValue('01/09/2026');
  });
});
