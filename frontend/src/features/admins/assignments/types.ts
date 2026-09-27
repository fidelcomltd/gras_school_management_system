import type { components } from '@/api/schema';

export type RoleAssignmentDto = components['schemas']['RoleAssignmentDto'];
export type CreateRoleAssignmentCommand = components['schemas']['CreateRoleAssignmentCommand'];

export const AssignmentsKeys = {
  List: 'admins.assignments',
  Create: 'admins.assignments.create',
  Revoke: 'admins.assignments.revoke',
} as const;
