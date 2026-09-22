import { zodResolver } from '@hookform/resolvers/zod';
import { FormProvider, useForm } from 'react-hook-form';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ApiError } from '@/lib/http';
import { useUpdatePupil } from '../api';
import { biographicalSchema, type BiographicalFormValues } from '../pupil-schema';
import type { PupilDto } from '../types';
import { BiographicalFields } from './biographical-fields';

/**
 * `PATCH /api/v1/pupils/{id}` (spec 6.5.10). Only changed fields are sent: `null` means unchanged on the wire, and an
 * emptied optional field goes as "" to clear it. The registration number is not editable here; it has its own reasoned
 * correction.
 */
export function EditPupilDialog({ pupil, onClose }: { pupil: PupilDto; onClose: () => void }) {
  const update = useUpdatePupil(pupil.id);
  const form = useForm<BiographicalFormValues>({
    resolver: zodResolver(biographicalSchema),
    defaultValues: {
      surname: pupil.surname,
      firstName: pupil.firstName,
      middleName: pupil.middleName ?? '',
      sex: pupil.sex,
      dateOfBirth: pupil.dateOfBirth,
      nationality: pupil.nationality,
      stateOfOrigin: pupil.stateOfOrigin,
      lga: pupil.lga,
      homeAddress: pupil.homeAddress,
      previousSchool: pupil.previousSchool ?? '',
      previousClass: pupil.previousClass ?? '',
      otherInformation: pupil.otherInformation ?? '',
    },
  });

  const onSubmit = form.handleSubmit((values) => {
    const dirty = form.formState.dirtyFields;
    const changed = <K extends keyof BiographicalFormValues>(key: K): BiographicalFormValues[K] | null =>
      dirty[key] ? values[key] : null;
    update.mutate(
      {
        id: pupil.id,
        surname: changed('surname'),
        firstName: changed('firstName'),
        middleName: changed('middleName'),
        sex: changed('sex'),
        dateOfBirth: changed('dateOfBirth'),
        nationality: changed('nationality'),
        stateOfOrigin: changed('stateOfOrigin'),
        lga: changed('lga'),
        homeAddress: changed('homeAddress'),
        previousSchool: changed('previousSchool'),
        previousClass: changed('previousClass'),
        otherInformation: changed('otherInformation'),
        registrationNumber: null,
      },
      { onSuccess: onClose },
    );
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Edit pupil</DialogTitle>
        </DialogHeader>

        <FormProvider {...form}>
          <form onSubmit={onSubmit} className="flex max-h-[70vh] flex-col gap-5 overflow-y-auto" noValidate>
            <FormError message={update.error instanceof ApiError ? update.error.message : null} />
            <BiographicalFields />
            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" disabled={update.isPending || !form.formState.isDirty}>
                {update.isPending ? 'Saving…' : 'Save changes'}
              </Button>
            </DialogFooter>
          </form>
        </FormProvider>
      </DialogContent>
    </Dialog>
  );
}
