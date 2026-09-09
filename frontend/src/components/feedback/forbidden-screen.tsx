/**
 * Rendered by `RequirePrivilege` (`app/router/require-privilege.tsx`) for a
 * route that exists but the signed-in caller's privileges do not permit
 * (TASK-0041 AC-3). Deliberately distinct from `NotFoundScreen` — the route
 * is real, the caller just cannot use it.
 */
export function ForbiddenScreen() {
  return (
    <div role="alert" className="flex flex-col items-start gap-2">
      <h1 className="font-display text-2xl font-semibold text-foreground">Access denied</h1>
      <p className="text-sm text-muted-foreground">
        You do not have permission to view this page. Contact an administrator if you believe
        this is a mistake.
      </p>
    </div>
  );
}
