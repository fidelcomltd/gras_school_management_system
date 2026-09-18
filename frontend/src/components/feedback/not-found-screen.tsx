/**
 * `*` fallback — a path that matches no route at all. Distinct from
 * `ForbiddenScreen` (a route that exists but the caller's privileges don't
 * permit): this one exists so the two are never confused with each other
 * (TASK-0041 AC-3).
 */
export function NotFoundScreen() {
  return (
    <div className="flex flex-col items-start gap-2">
      <h1 className="font-display text-2xl font-semibold text-foreground">Page not found</h1>
      <p className="text-sm text-muted-foreground">
        The address you followed does not match any screen in the portal.
      </p>
    </div>
  );
}
