import { useId, type ReactNode } from 'react';
import { Controller, useFormContext, useWatch } from 'react-hook-form';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { SearchableSelect } from '@/components/ui/searchable-select';
import { useNigerianGeography } from '../api';
import type { BiographicalFormValues } from '../pupil-schema';

/**
 * State of origin and LGA (spec 6.5.4), chosen from the server's own list rather than typed: searchable, since there are
 * 37 states and up to 44 LGAs in one. Choosing a state narrows the LGAs to it, and any change of state clears the LGA:
 * several states share LGA names (Irepodun in Kwara and Osun, Surulere in Lagos and Oyo) that are different places. A
 * value already on file that the list lacks still shows, so an edit never silently blanks it.
 */
export function GeographyFields() {
  const stateId = useId();
  const lgaId = useId();
  const geography = useNigerianGeography();
  const {
    control,
    setValue,
    formState: { errors },
  } = useFormContext<BiographicalFormValues>();
  const state = useWatch({ control, name: 'stateOfOrigin' });

  if (geography.isPending) return <LoadingState label="Loading states and LGAs…" className="py-2 sm:col-span-2" />;
  if (geography.isError) {
    return (
      <div className="sm:col-span-2">
        <QueryErrorState error={geography.error} onRetry={() => void geography.refetch()} />
      </div>
    );
  }

  const states = geography.data.states;
  const lgas = states.find((candidate) => candidate.name === state)?.lgas ?? [];

  const field = (
    id: string,
    label: string,
    message: string | undefined,
    renderControl: (describedBy: string | undefined) => ReactNode,
  ) => (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium text-foreground">
        {label}
      </label>
      {renderControl(message ? `${id}-error` : undefined)}
      {message ? (
        <p id={`${id}-error`} role="alert" className="text-xs font-medium text-destructive">
          {message}
        </p>
      ) : null}
    </div>
  );

  return (
    <>
      {field(stateId, 'State of origin', errors.stateOfOrigin?.message, (describedBy) => (
        <Controller
          control={control}
          name="stateOfOrigin"
          render={({ field: stateField }) => (
            <SearchableSelect
              id={stateId}
              label="State of origin"
              describedBy={describedBy}
              options={states.map((candidate) => candidate.name)}
              value={stateField.value}
              invalid={!!errors.stateOfOrigin}
              placeholder="Search states…"
              onChange={(next) => {
                if (next === stateField.value) return;
                stateField.onChange(next);
                setValue('lga', '', { shouldDirty: true });
              }}
            />
          )}
        />
      ))}
      {field(lgaId, 'LGA', errors.lga?.message, (describedBy) => (
        <Controller
          control={control}
          name="lga"
          render={({ field: lgaField }) => (
            <SearchableSelect
              id={lgaId}
              label="LGA"
              describedBy={describedBy}
              options={lgas}
              value={lgaField.value}
              invalid={!!errors.lga}
              disabled={!state}
              placeholder={state ? 'Search LGAs…' : 'Choose the state first'}
              onChange={lgaField.onChange}
            />
          )}
        />
      ))}
    </>
  );
}
