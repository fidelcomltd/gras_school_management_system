import type { components } from '@/api/schema';

export type RoleAssignmentDto = components['schemas']['RoleAssignmentDto'];
export type CreateRoleAssignmentCommand = components['schemas']['CreateRoleAssignmentCommand'];
export type CopyAssignmentsToSessionCommand = components['schemas']['CopyAssignmentsToSessionCommand'];
export type AssignmentCopyResultDto = components['schemas']['AssignmentCopyResultDto'];
export type AssignmentCopyRowDto = components['schemas']['AssignmentCopyRowDto'];

export const AssignmentsKeys = {
  List: 'admins.assignments',
  Create: 'admins.assignments.create',
  Revoke: 'admins.assignments.revoke',
  Copy: 'admins.assignments.copy',
} as const;
