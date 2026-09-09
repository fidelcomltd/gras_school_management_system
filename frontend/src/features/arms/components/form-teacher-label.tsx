/**
 * Renders an arm's form teacher honestly (TASK-0045's judgement call — see
 * `hooks/use-form-teacher-names.ts`'s own doc comment for the resolution
 * strategy). NEVER renders the bare `formTeacherAdminId` GUID: a name if one
 * was resolved, otherwise a message that tells the reader why not, distinct
 * from "no one is assigned".
 */
export function FormTeacherLabel({
  formTeacherAdminId,
  name,
  canResolve,
}: {
  formTeacherAdminId: string | null;
  name: string | undefined;
  canResolve: boolean;
}) {
  if (formTeacherAdminId === null) {
    return <span className="text-muted-foreground">No form teacher assigned</span>;
  }
  if (name) {
    return <span className="text-foreground">{name}</span>;
  }
  if (!canResolve) {
    return <span className="text-muted-foreground italic">Assigned — name not visible to you</span>;
  }
  return <span className="text-muted-foreground italic">Assigned — resolving name…</span>;
}
