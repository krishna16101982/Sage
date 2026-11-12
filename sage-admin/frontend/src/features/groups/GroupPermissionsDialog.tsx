"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Alert } from "@/components/ui/Form";
import { Modal } from "@/components/ui/Modal";
import { usePermission } from "@/hooks/usePermission";
import { getGroup, setGroupPermissions } from "@/services/groupService";
import { ApiError } from "@/types/api";
import type { GroupDetail, GroupItem, GroupMatrixModule, GroupMatrixPermission } from "@/types/group";
import { Perm } from "@/utils/permissionCodes";

/** Common actions first, then anything else alphabetically. */
const ACTION_ORDER = ["View", "Add", "Edit", "Delete", "Export"];
const actionRank = (a: string) => {
  const i = ACTION_ORDER.indexOf(a);
  return i === -1 ? ACTION_ORDER.length : i;
};

/**
 * Permission matrix for one group: modules as rows, actions as columns. Everything shown comes from the API (SQL).
 * Without Group.AssignPermissions the matrix is read-only. An inactive permission the group still holds can be removed
 * but not granted again.
 */
export function GroupPermissionsDialog({ group, onClose, onSaved }: { group: GroupItem; onClose: () => void; onSaved: () => void }) {
  const canAssign = usePermission(Perm.GroupAssignPermissions);
  const [detail, setDetail] = useState<GroupDetail | null>(null);
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [original, setOriginal] = useState<Set<number>>(new Set());
  const [filter, setFilter] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);

  const apply = (d: GroupDetail) => {
    const assigned = new Set(d.modules.flatMap((m) => m.permissions.filter((p) => p.assigned).map((p) => p.id)));
    setDetail(d);
    setSelected(assigned);
    setOriginal(assigned);
  };

  const load = useCallback(async () => {
    try {
      apply(await getGroup(group.id));
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load the group's permissions.");
    }
  }, [group.id]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, [load]);

  const actions = useMemo(() => {
    const all = new Set(detail?.modules.flatMap((m) => m.permissions.map((p) => p.action)) ?? []);
    return [...all].sort((a, b) => actionRank(a) - actionRank(b) || a.localeCompare(b));
  }, [detail]);

  const visibleModules = useMemo(() => {
    const q = filter.trim().toLowerCase();
    const mods = detail?.modules ?? [];
    return q ? mods.filter((m) => `${m.name} ${m.code} ${m.category ?? ""}`.toLowerCase().includes(q)) : mods;
  }, [detail, filter]);

  // Rows grouped under their category, keeping the API's order.
  const byCategory = useMemo(() => {
    const groups: { category: string; modules: GroupMatrixModule[] }[] = [];
    for (const m of visibleModules) {
      const category = m.category ?? "Other";
      const last = groups[groups.length - 1];
      if (last && last.category === category) last.modules.push(m);
      else groups.push({ category, modules: [m] });
    }
    return groups;
  }, [visibleModules]);

  const editable = (p: GroupMatrixPermission) => canAssign && (p.isActive || original.has(p.id));
  const grantable = (perms: GroupMatrixPermission[]) => perms.filter((p) => editable(p) && (p.isActive || selected.has(p.id)));

  const toggle = (ids: number[], on: boolean) => {
    setSaved(false);
    setSelected((prev) => {
      const next = new Set(prev);
      ids.forEach((id) => (on ? next.add(id) : next.delete(id)));
      return next;
    });
  };

  const columnPermissions = (action: string) =>
    visibleModules.flatMap((m) => m.permissions.filter((p) => p.action === action));

  const added = [...selected].filter((id) => !original.has(id)).length;
  const removed = [...original].filter((id) => !selected.has(id)).length;
  const dirty = added + removed > 0;

  async function onSave() {
    setError(null);
    setBusy(true);
    try {
      apply(await setGroupPermissions(group.id, [...selected]));
      setSaved(true);
      onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  const cell = "center";

  return (
    <Modal title={`Permissions: ${group.name}`} onClose={onClose} size="xl">
      <div className="space-y-3 text-sm">
        {error && <Alert kind="error">{error}</Alert>}
        {saved && !dirty && <Alert kind="success">Permissions saved. Members get the new access on their next request.</Alert>}
        {group.status === "Inactive" && <Alert kind="error">This group is inactive, so it grants nothing until it is activated again.</Alert>}
        {!canAssign && <p className="text-muted">Read-only: you do not have permission to change group permissions.</p>}

        <div className="toolbar">
          <input
            aria-label="Filter modules"
            placeholder="Filter modules"
            className="field field-inline w-56"
            value={filter}
            onChange={(e) => setFilter(e.target.value)}
          />
          <span className="text-muted">
            {selected.size} selected{dirty && ` (${added} added, ${removed} removed)`}
          </span>
        </div>

        {!detail && !error && <p className="table-empty">Loading...</p>}

        {detail && (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Module</th>
                  <th className={cell}>All</th>
                  {actions.map((a) => {
                    const perms = grantable(columnPermissions(a));
                    const allOn = perms.length > 0 && perms.every((p) => selected.has(p.id));
                    return (
                      <th key={a} className={cell}>
                        <label className="flex flex-col items-center gap-1 font-normal">
                          <span className="font-semibold">{a}</span>
                          {canAssign && (
                            <input
                              type="checkbox"
                              aria-label={`All ${a} permissions`}
                              checked={allOn}
                              disabled={perms.length === 0}
                              onChange={(e) => toggle(perms.map((p) => p.id), e.target.checked)}
                            />
                          )}
                        </label>
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody>
                {byCategory.map(({ category, modules }) => [
                  <tr key={`c-${category}`} className="group-row">
                    <td colSpan={actions.length + 2}>
                      {category}
                    </td>
                  </tr>,
                  ...modules.map((m) => {
                    const perms = grantable(m.permissions);
                    const allOn = perms.length > 0 && perms.every((p) => selected.has(p.id));
                    return (
                      <tr key={m.id}>
                        <td>
                          {m.name}
                          {!m.isActive && <span className="badge">inactive module</span>}
                        </td>
                        <td className={cell}>
                          {canAssign && (
                            <input
                              type="checkbox"
                              aria-label={`All ${m.name} permissions`}
                              checked={allOn}
                              disabled={perms.length === 0}
                              onChange={(e) => toggle(perms.map((p) => p.id), e.target.checked)}
                            />
                          )}
                        </td>
                        {actions.map((a) => {
                          const p = m.permissions.find((x) => x.action === a);
                          if (!p) return <td key={a} className={cell} />;
                          const on = selected.has(p.id);
                          const canToggle = editable(p) && (p.isActive || on);
                          return (
                            <td key={a} className={cell}>
                              <input
                                type="checkbox"
                                aria-label={p.code}
                                title={p.isActive ? (p.description ?? p.name) : `${p.name} (inactive: can be removed, not granted again)`}
                                checked={on}
                                disabled={!canToggle}
                                onChange={(e) => toggle([p.id], e.target.checked)}
                                className={p.isActive ? "" : "opacity-50"}
                              />
                            </td>
                          );
                        })}
                      </tr>
                    );
                  }),
                ])}
                {visibleModules.length === 0 && (
                  <tr>
                    <td colSpan={actions.length + 2} className="table-empty">
                      No modules match.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

        <div className="modal-footer">
          <button type="button" onClick={onClose} className="btn btn-secondary">
            {canAssign && dirty ? "Cancel" : "Close"}
          </button>
          {canAssign && (
            <>
              <button type="button" disabled={!dirty || busy} onClick={() => detail && apply(detail)} className="btn btn-secondary">
                Reset
              </button>
              <button
                type="button"
                disabled={!dirty || busy}
                onClick={onSave}
                className="btn btn-primary"
              >
                {busy ? "Saving..." : "Save permissions"}
              </button>
            </>
          )}
        </div>
      </div>
    </Modal>
  );
}
