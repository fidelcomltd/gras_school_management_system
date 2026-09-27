import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiGet, apiPost } from '@/api/client';
import { AdminsKeys } from '../types';
import { AssignmentsKeys, type CreateRoleAssignmentCommand } from './types';

const ASSIGNMENTS_PATH = '/api/v1/admins/{id}/assignments';
const ASSIGNMENT_PATH = '/api/v1/assignments/{id}';

/** Every assignment on the account, active and revoked (spec 6.1.5). Gated `admin.view`; each carries its role, session and class names. */
export function useAssignments(adminId: string) {
  return useQuery({
    queryKey: [AssignmentsKeys.List, adminId],
    queryFn: ({ signal }) => apiGet(ASSIGNMENTS_PATH, undefined, { pathParams: { id: adminId }, signal }),
  });
}

/**
 * Grants a role for a session, school-wide (`role.assign`) or over chosen arms (`role.scope.assign`). The server enforces
 * escalation rules 1 and 3 (no self-assignment, nothing wider than the caller's own scope) and says why in words.
 */
export function useCreateAssignment(adminId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AssignmentsKeys.Create, adminId],
    mutationFn: (body: Omit<CreateRoleAssignmentCommand, 'adminAccountId'>) =>
      apiPost(ASSIGNMENTS_PATH, { ...body, adminAccountId: adminId }, { pathParams: { id: adminId }, idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [AssignmentsKeys.List, adminId] });
      // The admin list shows each account's roles and scope.
      void queryClient.invalidateQueries({ queryKey: [AdminsKeys.List] });
    },
  });
}

/** Revokes one assignment (spec 6.1.14). Gated `role.assign`; never your own (rule 1). */
export function useRevokeAssignment(adminId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AssignmentsKeys.Revoke, adminId],
    mutationFn: (assignmentId: string) =>
      apiDelete(ASSIGNMENT_PATH, { pathParams: { id: assignmentId }, idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [AssignmentsKeys.List, adminId] });
      // The admin list shows each account's roles and scope.
      void queryClient.invalidateQueries({ queryKey: [AdminsKeys.List] });
    },
  });
}
