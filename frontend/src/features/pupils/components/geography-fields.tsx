import { useId } from 'react';
import { Controller, useFormContext } from 'react-hook-form';
import { SearchableSelect } from '@/components/ui/searchable-select';
import { Spinner } from '@/components/ui/spinner';
import { useNigerianGeography } from '../api';
import type { BiographicalFormValues } from '../pupil-schema';

/**
 * State of origin and LGA (spec 6.5.4), chosen from the server's own list rather than typed: searchable, since there are
 * 37 states and up to 44 LGAs in one. Choosing a state narrows the LGAs to it, and a state change clears an LGA that
 * does not belong to the new state. A value already on file that the list lacks still shows, so an edit never silently
 * blanks it.
 */
export function GeographyFields() {
  const stateId = useId();
  const lgaId = useId();
  const geography = useNigerianGeography();
  const {
    control,
    watch,
    setValue,
    formState: { errors },
  } = useFormContext<BiographicalFormValues>();
  const state = watch('stateOfOrigin');

  const states = geography.data?.states ?? [];
  const lgas = states.find((candidate) => candidate.name === state)?.lgas ?? [];

  if (geography.isPending) {
    return (
      <output className="flex items-center gap-2 text-sm text-muted-foreground sm:col-span-2">
        <Spinner className="size-4" />
        Loading states and LGAs…
      </output>
    );
  }

  return (
    <>
      <div className="flex flex-col gap-1.5">
        <label htmlFor={stateId} className="text-sm font-medium text-foreground">
          State of origin
        </label>
        <Controller
          control={control}
          name="stateOfOrigin"
          render={({ field }) => (
            <SearchableSelect
              id={stateId}
              label="State of origin"
              options={states.map((candidate) => candidate.name)}
              value={field.value}
              invalid={!!errors.stateOfOrigin}
              placeholder="Search states…"
              onChange={(next) => {
                field.onChange(next);
                const nextLgas = states.find((candidate) => candidate.name === next)?.lgas ?? [];
                if (!nextLgas.includes(watch('lga'))) setValue('lga', '', { shouldDirty: true });
              }}
            />
          )}
        />
        {errors.stateOfOrigin ? (
          <p role="alert" className="text-xs font-medium text-destructive">
            {errors.stateOfOrigin.message}
          </p>
        ) : null}
      </div>
      <div className="flex flex-col gap-1.5">
        <label htmlFor={lgaId} className="text-sm font-medium text-foreground">
          LGA
        </label>
        <Controller
          control={control}
          name="lga"
          render={({ field }) => (
            <SearchableSelect
              id={lgaId}
              label="LGA"
              options={lgas}
              value={field.value}
              invalid={!!errors.lga}
              disabled={!state}
              placeholder={state ? 'Search LGAs…' : 'Choose the state first'}
              onChange={field.onChange}
            />
          )}
        />
        {errors.lga ? (
          <p role="alert" className="text-xs font-medium text-destructive">
            {errors.lga.message}
          </p>
        ) : null}
      </div>
    </>
  );
}
