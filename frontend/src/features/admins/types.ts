import type { components } from '@/api/schema';

/**
 * Query/mutation keys for the Admins feature (TASK-0043), mirroring
 * `features/sessions/types.ts`'s shape. A `const` object, not a TS `enum` —
 * `erasableSyntaxOnly` disallows `enum`.
 */
export const AdminsKeys = {
  List: 'admins.list',
  Detail: 'admins.detail',
  Create: 'admins.create',
  Update: 'admins.update',
  ChangeStatus: 'admins.changeStatus',
  ResetPassword: 'admins.resetPassword',
  RevokeSessions: 'admins.revokeSessions',
} as const;

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type AdminAccountSummaryDto = components['schemas']['AdminAccountSummaryDto'];
export type AdminAccountDetailDto = components['schemas']['AdminAccountDetailDto'];
export type AdminAccountStatus = components['schemas']['AdminAccountStatus'];
export type CreateAdminAccountCommand = components['schemas']['CreateAdminAccountCommand'];
export type CreateAdminAccountResponse = components['schemas']['CreateAdminAccountResponse'];
export type UpdateAdminAccountCommand = components['schemas']['UpdateAdminAccountCommand'];
export type ChangeAdminAccountStatusCommand = components['schemas']['ChangeAdminAccountStatusCommand'];
export type ResetAdminAccountPasswordResponse = components['schemas']['ResetAdminAccountPasswordResponse'];
