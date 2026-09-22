import type { components } from '@/api/schema';

/** Query/mutation keys for the Subjects feature (subjects and the level mapping grid). */
export const SubjectsKeys = {
  List: 'subjects.list',
  Create: 'subjects.create',
  Update: 'subjects.update',
  Delete: 'subjects.delete',
  Grid: 'subjects.grid',
  SaveGrid: 'subjects.saveGrid',
  Copy: 'subjects.copy',
  Prefill: 'subjects.prefill',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type SubjectDto = components['schemas']['SubjectDto'];
export type SubjectStatus = components['schemas']['SubjectStatus'];
export type CreateSubjectCommand = components['schemas']['CreateSubjectCommand'];
export type UpdateSubjectCommand = components['schemas']['UpdateSubjectCommand'];
export type SubjectMappingGridDto = components['schemas']['SubjectMappingGridDto'];
export type SubjectMappingGridEntryInput = components['schemas']['SubjectMappingGridEntryInput'];
export type SaveSubjectMappingGridResponse = components['schemas']['SaveSubjectMappingGridResponse'];
export type SubjectMappingChangeDto = components['schemas']['SubjectMappingChangeDto'];

/** A mapping cell's key in the editable grid. */
export const cellKey = (subjectId: string, classLevelId: string) => `${subjectId}:${classLevelId}`;
