import { FormProvider, useForm, useWatch } from 'react-hook-form';
import { describe, expect, it } from 'vitest';
import { renderWithProviders, screen } from '@/test/render';
import { apiUrl, http, HttpResponse } from '@/test/msw/handlers';
import { server } from '@/test/msw/server';
import type { BiographicalFormValues } from '../pupil-schema';
import { GeographyFields } from './geography-fields';

function Harness({ state = '', lga = '' }: { state?: string; lga?: string }) {
  const form = useForm<BiographicalFormValues>({ defaultValues: { stateOfOrigin: state, lga } });
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
    expect(screen.getByRole('combobox', { name: 'LGA' })).toHaveValue('');
  });

  it('keeps showing a value already on file that the list lacks, so an edit never blanks it', async () => {
    renderWithProviders(<Harness state="Imo" lga="Owerri Municipal" />);

    expect(await screen.findByRole('combobox', { name: 'State of origin' })).toHaveValue('Imo');
    expect(screen.getByRole('combobox', { name: 'LGA' })).toHaveValue('Owerri Municipal');
  });

  it('says so, with a retry, when the list cannot be loaded', async () => {
    server.use(http.get(apiUrl('/api/v1/geography/states'), () => new HttpResponse(null, { status: 500 })));
    renderWithProviders(<Harness />);

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument();
    expect(screen.queryByRole('combobox', { name: 'State of origin' })).not.toBeInTheDocument();
  });
});
