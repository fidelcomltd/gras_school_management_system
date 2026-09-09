import type { components } from '@/api/schema';

/**
 * Query/mutation keys for the Roles feature (TASK-0043). `GET /privileges`
 * lives here too rather than a separate `features/privileges/` folder — it
 * has exactly one consumer, the privilege picker used by this feature's own
 * create/edit dialogs, per the tag-per-folder rule's own precedent for
 * co-locating things that only ever change together (see `sessions/types.ts`
 * for terms living inside `sessions/`, same reasoning).
 */
export const RolesKeys = {
  List: 'roles.list',
  Detail: 'roles.detail',
  Create: 'roles.create',
  Update: 'roles.update',
  Delete: 'roles.delete',
  Privileges: 'privileges.register',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type RoleDto = components['schemas']['RoleDto'];
export type RoleStatus = components['schemas']['RoleStatus'];
export type CreateRoleCommand = components['schemas']['CreateRoleCommand'];
export type UpdateRoleCommand = components['schemas']['UpdateRoleCommand'];
export type PrivilegeGroupDto = components['schemas']['PrivilegeGroupDto'];
export type PrivilegeDescriptorDto = components['schemas']['PrivilegeDescriptorDto'];
