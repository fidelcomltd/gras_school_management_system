import type { PrivilegeGroupDto } from '../types';

/**
 * `GET /privileges` (TASK-0028 §1) already groups the 93-row register by
 * module — this renders those groups as-is rather than re-deriving a
 * grouping client-side. Per the card's own cut line, a flat checkbox list
 * with no search satisfies the contract; this keeps the module grouping
 * (the one AC that names it explicitly) and cuts everything past that.
 *
 * `value` may contain a code absent from every group — a role's existing
 * privileges can include one the current register does not recognise (a
 * legacy alias, or a register that has moved on). Root CLAUDE.md §8: the
 * client tolerates an unknown member rather than dropping it silently or
 * crashing, so it is rendered in its own bucket, still togglable (removal
 * is always allowed — spec 6.1.7 rule 2 only restricts additions).
 */
export function PrivilegePicker({
  groups,
  value,
  onChange,
}: {
  groups: PrivilegeGroupDto[];
  value: string[];
  onChange: (next: string[]) => void;
}) {
  const known = new Set(groups.flatMap((group) => group.privileges.map((privilege) => privilege.code)));
  const unknown = value.filter((code) => !known.has(code));

  function toggle(code: string, checked: boolean): void {
    onChange(checked ? [...value, code] : value.filter((existing) => existing !== code));
  }

  return (
    <div className="flex max-h-64 flex-col gap-4 overflow-y-auto rounded-md border border-border p-3">
      {groups.map((group) => (
        <fieldset key={group.key} className="flex flex-col gap-1.5">
          <legend className="text-sm font-medium text-foreground">{group.title}</legend>
          {group.privileges.map((privilege) => (
            <label key={privilege.code} className="flex items-start gap-2 text-sm text-foreground">
              <input
                type="checkbox"
                className="mt-0.5"
                checked={value.includes(privilege.code)}
                onChange={(e) => toggle(privilege.code, e.target.checked)}
              />
              <span>
                {privilege.code}
                <span className="block text-xs text-muted-foreground">{privilege.permits}</span>
              </span>
            </label>
          ))}
        </fieldset>
      ))}
      {unknown.length > 0 ? (
        <fieldset className="flex flex-col gap-1.5">
          <legend className="text-sm font-medium text-foreground">Other (not in the current register)</legend>
          {unknown.map((code) => (
            <label key={code} className="flex items-center gap-2 text-sm text-foreground">
              <input type="checkbox" checked onChange={() => toggle(code, false)} />
              {code}
            </label>
          ))}
        </fieldset>
      ) : null}
    </div>
  );
}
