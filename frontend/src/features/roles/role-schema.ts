import { z } from 'zod';

/**
 * Client-side mirror of the backend's role validation (spec 6.1.4). Where
 * the two diverge the backend is authoritative — its own rejection (reserved
 * name, unknown privilege code, rule-2 privilege-escalation) still surfaces
 * verbatim through `error.message`; this only catches the obvious cases
 * before a round trip.
 */
const NAME = z.string().min(1, 'Name is required.').max(60, 'At most 60 characters.');
const DESCRIPTION = z.string().max(300, 'At most 300 characters.');
const PRIVILEGES = z.array(z.string()).min(1, 'Choose at least one privilege.');

export const createRoleSchema = z.object({
  name: NAME,
  description: DESCRIPTION,
  privileges: PRIVILEGES,
});

export type CreateRoleFormValues = z.infer<typeof createRoleSchema>;

/**
 * `status` is a hardcoded literal union rather than the generated
 * `RoleStatus` re-export, matching `classes/level-schema.ts`'s own
 * `editLevelSchema` precedent — a form values type, not a wire type.
 */
export const editRoleSchema = z.object({
  name: NAME,
  description: DESCRIPTION,
  privileges: PRIVILEGES,
  status: z.enum(['Active', 'Archived']),
});

export type EditRoleFormValues = z.infer<typeof editRoleSchema>;
