import { zodResolver } from '@hookform/resolvers/zod';
import { useState } from 'react';
import { FormProvider, useForm, type DefaultValues } from 'react-hook-form';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ApiError } from '@/lib/http';
import { fetchDuplicates, useCreatePupil } from '../api';
import { createPupilSchema, orNull, type CreatePupilFormValues } from '../pupil-schema';
import { pupilName, type PupilDto } from '../types';
import { AdmissionFields } from './admission-fields';
import { BiographicalFields } from './biographical-fields';

// No default sex: the admin must choose.
const EMPTY: DefaultValues<CreatePupilFormValues> = {
  surname: '',
  firstName: '',
  middleName: '',
  dateOfBirth: '',
  nationality: '',
  stateOfOrigin: '',
  lga: '',
  homeAddress: '',
  previousSchool: '',
  previousClass: '',
  otherInformation: '',
  classAdmittedInto: '',
  admissionType: 'New',
  dateApplicationReceived: '',
  assessmentRequired: false,
};

/**
 * `POST /api/v1/pupils` (spec 6.5.4): creates a Pending pupil with no registration number; approval in the admissions
 * queue issues one. Before creating, it checks for a pupil with the same name and date of birth (6.5.5) and asks for a
 * second click if one exists, rather than silently creating a duplicate.
 */
export function CreatePupilDialog({ onClose, onCreated }: { onClose: () => void; onCreated: (pupil: PupilDto) => void }) {
  const createPupil = useCreatePupil();
  const form = useForm<CreatePupilFormValues>({ resolver: zodResolver(createPupilSchema), defaultValues: EMPTY });
  const [duplicates, setDuplicates] = useState<PupilDto[] | null>(null);
  const [checkError, setCheckError] = useState<string | null>(null);

  const create = (values: CreatePupilFormValues) =>
    createPupil.mutate(
      {
        surname: values.surname.trim(),
        firstName: values.firstName.trim(),
        middleName: orNull(values.middleName),
        sex: values.sex,
        dateOfBirth: values.dateOfBirth,
        nationality: orNull(values.nationality),
        stateOfOrigin: values.stateOfOrigin.trim(),
        lga: values.lga.trim(),
        homeAddress: values.homeAddress.trim(),
        previousSchool: orNull(values.previousSchool),
        previousClass: orNull(values.previousClass),
        otherInformation: orNull(values.otherInformation),
        admission: {
          sessionId: null,
          dateApplicationReceived: values.dateApplicationReceived || null,
          dateAdmitted: null,
          classAdmittedInto: values.classAdmittedInto,
          admissionType: values.admissionType,
          admissionTypeNote: null,
          assessmentRequired: values.assessmentRequired,
        },
      },
      { onSuccess: onCreated },
    );

  const onSubmit = form.handleSubmit(async (values) => {
    if (duplicates !== null) {
      create(values);
      return;
    }

    setCheckError(null);
    try {
      const found = await fetchDuplicates(values.surname.trim(), values.firstName.trim(), values.dateOfBirth);
      if (found.length > 0) {
        setDuplicates(found);
        return;
      }
    } catch (error) {
      setCheckError(error instanceof ApiError ? error.message : 'Could not check for duplicates.');
      return;
    }

    create(values);
  });

  const formError = checkError ?? (createPupil.error instanceof ApiError ? createPupil.error.message : null);

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>New pupil</DialogTitle>
        </DialogHeader>

        <FormProvider {...form}>
          <form onSubmit={onSubmit} className="flex max-h-[70vh] flex-col gap-5 overflow-y-auto" noValidate>
            <FormError message={formError} />
            <BiographicalFields />
            <AdmissionFields />

            {duplicates ? (
              <div role="alert" className="flex flex-col gap-2 rounded-md bg-warning/10 px-3 py-2 text-sm text-foreground">
                <p className="font-medium">A pupil with this name and date of birth already exists:</p>
                <ul className="list-disc pl-5">
                  {duplicates.map((pupil) => (
                    <li key={pupil.id}>
                      {pupilName(pupil)} ({pupil.registrationNumber ?? 'no number yet'}, {pupil.status})
                    </li>
                  ))}
                </ul>
                <p>Check this is a different child before creating another record.</p>
              </div>
            ) : null}

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" disabled={createPupil.isPending || form.formState.isSubmitting}>
                {createPupil.isPending ? 'Creating…' : duplicates ? 'Create anyway' : 'Create pupil'}
              </Button>
            </DialogFooter>
          </form>
        </FormProvider>
      </DialogContent>
    </Dialog>
  );
}
