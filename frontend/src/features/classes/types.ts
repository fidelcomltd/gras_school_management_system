import type { components } from '@/api/schema';

/**
 * Query/mutation keys for the Classes feature. Levels and Sections share this
 * one folder (`features/classes/`, per TASK-0042's explicit scope) rather
 * than the two the tag-per-folder rule in `src/features/README.md` would
 * otherwise suggest (`Levels`/`Sections` are separate OpenAPI tags): a
 * section is only ever managed from the same screen as the level chain it
 * feeds, and per `.agent/STATE.md`'s TASK-0038 entry the backend itself
 * gates section mutations under `level.*` privileges rather than inventing
 * `section.*` ones — the two are already one register-level concept.
 */
export const ClassesKeys = {
  Levels: 'classes.levels',
  Level: 'classes.level',
  CreateLevel: 'classes.createLevel',
  UpdateLevel: 'classes.updateLevel',
  DeleteLevel: 'classes.deleteLevel',
  ReorderLevels: 'classes.reorderLevels',
  Sections: 'classes.sections',
  CreateSection: 'classes.createSection',
  UpdateSection: 'classes.updateSection',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type LevelDto = components['schemas']['LevelDto'];
export type LevelStatus = components['schemas']['LevelStatus'];
export type CreateLevelCommand = components['schemas']['CreateLevelCommand'];
export type UpdateLevelCommand = components['schemas']['UpdateLevelCommand'];
export type ReorderLevelsCommand = components['schemas']['ReorderLevelsCommand'];
export type SectionDto = components['schemas']['SectionDto'];
export type CreateSectionCommand = components['schemas']['CreateSectionCommand'];
export type UpdateSectionCommand = components['schemas']['UpdateSectionCommand'];
