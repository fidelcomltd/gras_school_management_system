import { FormProvider, useForm, useWatch } from 'react-hook-form';
import { describe, expect, it } from 'vitest';
import { renderWithProviders, screen } from '@/test/render';
import type { BiographicalFormValues } from '../pupil-schema';
import { GeographyFields } from './geography-fields';

function Harness() {
  const form = useForm<BiographicalFormValues>({ defaultValues: { stateOfOrigin: '', lga: '' } });
  const values = useWatch({ control: form.control });
  return (
    <FormProvider {...form}>
      <GeographyFields />
      <output aria-label="form values">{`${values.stateOfOrigin ?? ''}|${values.lga ?? ''}`}</output>
    </FormProvider>
  );
}

// GET /geography/states answers with the contract example: Abia and Anambra, three LGAs each.
describe('GeographyFields', () => {
  it("offers only the chosen state's LGAs, and clears an LGA the new state lacks", async () => {
    const { user } = renderWithProviders(<Harness />);

    expect(await screen.findByRole('combobox', { name: 'LGA' })).toBeDisabled();

    await user.click(screen.getByRole('combobox', { name: 'State of origin' }));
    await user.click(await screen.findByRole('option', { name: 'Anambra' }));
    await user.click(screen.getByRole('combobox', { name: 'LGA' }));
    expect(await screen.findByRole('option', { name: 'Awka South' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Aba North' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', { name: 'Awka South' }));
    expect(screen.getByRole('status', { name: 'form values' })).toHaveTextContent('Anambra|Awka South');

    await user.click(screen.getByRole('combobox', { name: 'State of origin' }));
    await user.clear(screen.getByRole('combobox', { name: 'State of origin' }));
    await user.type(screen.getByRole('combobox', { name: 'State of origin' }), 'Abi');
    await user.click(await screen.findByRole('option', { name: 'Abia' }));

    expect(screen.getByRole('status', { name: 'form values' })).toHaveTextContent(/^Abia\|$/);
  });
});
