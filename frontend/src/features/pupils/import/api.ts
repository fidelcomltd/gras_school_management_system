import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { components } from '@/api/schema';
import { getFile, postRequest, saveFile } from '@/lib/http';
import { PupilsKeys } from '../types';

/** Bulk import (spec 6.5.13): template, validate (writes nothing), commit (all or nothing). */

type S = components['schemas'];
type ReportDto = S['PupilImportReportDto'];
type RowDto = S['PupilImportRowDto'];
type WarningDto = S['PupilImportCapacityWarningDto'];
type ResultDto = S['PupilImportResultDto'];
type ImportedDto = S['PupilImportedDto'];

/** The contract types with their integers as numbers (the schema allows numeric strings); normalised once, here. */
export interface PupilImportRow extends Omit<RowDto, 'sheetRow'> {
  sheetRow: number;
}
export interface PupilImportCapacityWarning extends Omit<WarningDto, 'capacity' | 'currentCount' | 'importCount'> {
  capacity: number;
  currentCount: number;
  importCount: number;
}
export interface PupilImportReport
  extends Omit<ReportDto, 'totalRows' | 'acceptedCount' | 'rejectedCount' | 'registerMatchCount' | 'rows' | 'capacityWarnings'> {
  totalRows: number;
  acceptedCount: number;
  rejectedCount: number;
  registerMatchCount: number;
  rows: PupilImportRow[];
  capacityWarnings: PupilImportCapacityWarning[];
}
export interface PupilImportResult extends Omit<ResultDto, 'importedCount' | 'skippedCount' | 'pupils'> {
  importedCount: number;
  skippedCount: number;
  pupils: (Omit<ImportedDto, 'sheetRow'> & { sheetRow: number })[];
}

function toReport(dto: ReportDto): PupilImportReport {
  return {
    ...dto,
    totalRows: Number(dto.totalRows),
    acceptedCount: Number(dto.acceptedCount),
    rejectedCount: Number(dto.rejectedCount),
    registerMatchCount: Number(dto.registerMatchCount),
    rows: dto.rows.map((row) => ({ ...row, sheetRow: Number(row.sheetRow) })),
    capacityWarnings: dto.capacityWarnings.map((warning) => ({
      ...warning,
      capacity: Number(warning.capacity),
      currentCount: Number(warning.currentCount),
      importCount: Number(warning.importCount),
    })),
  };
}

function toResult(dto: ResultDto): PupilImportResult {
  return {
    ...dto,
    importedCount: Number(dto.importedCount),
    skippedCount: Number(dto.skippedCount),
    pupils: dto.pupils.map((pupil) => ({ ...pupil, sheetRow: Number(pupil.sheetRow) })),
  };
}

/** Up to 1000 rows run synchronously; well past the default timeout on a slow link, so give them room. */
const IMPORT_TIMEOUT_MS = 180_000;

export const ImportKeys = {
  Template: 'pupils.import.template',
  Validate: 'pupils.import.validate',
  Commit: 'pupils.import.commit',
} as const;

/** Gated `pupil.import`: the XLSX template with the active session's arms, saved through the browser. */
export function useDownloadImportTemplate() {
  return useMutation({
    mutationKey: [ImportKeys.Template],
    mutationFn: async () => saveFile(await getFile('/api/v1/pupils/import/template', 'pupil-import-template.xlsx')),
  });
}

/** The report for `file`. Nothing is written. */
export function useValidateImport() {
  return useMutation({
    mutationKey: [ImportKeys.Validate],
    mutationFn: async (file: File) => {
      const form = new FormData();
      form.append('file', file);
      return toReport(await postRequest<ReportDto, FormData>('/api/v1/pupils/import/validate', form, { timeout: IMPORT_TIMEOUT_MS }));
    },
  });
}

export interface CommitImportInput {
  file: File;
  fileSha256: string;
  skipRows: number[];
  createRows: number[];
  overrideCapacity: boolean;
}

/** The same file again with a decision for every register match. `Idempotency-Key` REQUIRED, fresh per submit. */
export function useCommitImport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ImportKeys.Commit],
    mutationFn: async (input: CommitImportInput) => {
      const form = new FormData();
      form.append('file', input.file);
      form.append('fileSha256', input.fileSha256);
      for (const row of input.skipRows) form.append('skipRows', String(row));
      for (const row of input.createRows) form.append('createRows', String(row));
      if (input.overrideCapacity) form.append('overrideCapacity', 'true');
      const result = await postRequest<ResultDto, FormData>('/api/v1/pupils/import/commit', form, {
        headers: { 'Idempotency-Key': crypto.randomUUID() },
        timeout: IMPORT_TIMEOUT_MS,
      });
      return toResult(result);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [PupilsKeys.List] });
    },
  });
}
