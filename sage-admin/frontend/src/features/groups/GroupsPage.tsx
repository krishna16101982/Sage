"use client";

import { useCallback, useEffect, useState } from "react";
import { Can } from "@/components/guards/Can";
import { Alert } from "@/components/ui/Form";
import { Pagination } from "@/components/ui/Pagination";
import { getGroups } from "@/services/groupService";
import { ApiError, type PagedResult } from "@/types/api";
import type { GroupItem, GroupQuery, GroupStatus } from "@/types/group";
import { Perm } from "@/utils/permissionCodes";
import { DeleteGroupDialog } from "./DeleteGroupDialog";
import { GroupFormDialog } from "./GroupFormDialog";
import { GroupPermissionsDialog } from "./GroupPermissionsDialog";

type Dialog =
  | { kind: "none" }
  | { kind: "form"; group?: GroupItem }
  | { kind: "permissions"; group: GroupItem }
  | { kind: "delete"; group: GroupItem };

type SortKey = NonNullable<GroupQuery["sortBy"]>;

/** Permission groups: list, search, create, edit, delete and the permission matrix. Data comes from SQL via the API. */
export function GroupsPage() {
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<"" | GroupStatus>("");
  const [sortBy, setSortBy] = useState<SortKey>("name");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<GroupItem> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState<Dialog>({ kind: "none" });
  const [refreshKey, setRefreshKey] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await getGroups({ page, pageSize: 15, search: search.trim() || undefined, status: status || undefined, sortBy, sortDirection }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load groups.");
    } finally {
      setLoading(false);
    }
  }, [page, search, status, sortBy, sortDirection]);

  // Debounce typing in the search box; reload when filters, sort, page or the refresh key change.
  useEffect(() => {
    const t = setTimeout(load, 250);
    return () => clearTimeout(t);
  }, [load, refreshKey]);

  const refresh = () => setRefreshKey((k) => k + 1);
  const closeAndRefresh = () => {
    setDialog({ kind: "none" });
    refresh();
  };

  const sortHeader = (key: SortKey, label: string) => {
    const active = sortBy === key;
    return (
      <th aria-sort={active ? (sortDirection === "asc" ? "ascending" : "descending") : "none"}>
        <button
          type="button"
          className="sort-button"
          onClick={() => {
            if (active) setSortDirection(sortDirection === "asc" ? "desc" : "asc");
            else {
              setSortBy(key);
              setSortDirection("asc");
            }
            setPage(1);
          }}
        >
          {label}
          {active && (sortDirection === "asc" ? " ▲" : " ▼")}
        </button>
      </th>
    );
  };

  const field = "field field-inline";

  return (
    <div className="space-y-4">
      <h1 className="page-title">Permission Groups</h1>

      <div className="toolbar">
        <input
          aria-label="Search groups"
          placeholder="Search name, code or description"
          className={`${field} w-64`}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        <select
          aria-label="Filter by status"
          className={field}
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as "" | GroupStatus);
            setPage(1);
          }}
        >
          <option value="">Any status</option>
          <option value="Active">Active</option>
          <option value="Inactive">Inactive</option>
        </select>
        <div className="toolbar-spacer" />
        <Can permission={Perm.GroupAdd}>
          <button
            type="button"
            onClick={() => setDialog({ kind: "form" })}
            className="btn btn-primary"
          >
            Add group
          </button>
        </Can>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              {sortHeader("name", "Name")}
              {sortHeader("code", "Code")}
              {sortHeader("userCount", "Users")}
              <th>Permissions</th>
              {sortHeader("status", "Status")}
              <th />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((g) => (
              <tr key={g.id}>
                <td>
                  {g.name}
                  {g.description && <div className="hint">{g.description}</div>}
                </td>
                <td className="code">{g.code}</td>
                <td>{g.userCount}</td>
                <td>{g.permissionCount}</td>
                <td>
                  {g.status}
                  {g.isSystem && <span className="badge">system</span>}
                </td>
                <td className="actions">
                  <button type="button" className="link-button" onClick={() => setDialog({ kind: "permissions", group: g })}>
                    Permissions
                  </button>
                  <Can permission={Perm.GroupEdit}>
                    <button type="button" className="link-button" onClick={() => setDialog({ kind: "form", group: g })}>
                      Edit
                    </button>
                  </Can>
                  {!g.isSystem && (
                    <Can permission={Perm.GroupDelete}>
                      <button type="button" className="link-button danger" onClick={() => setDialog({ kind: "delete", group: g })}>
                        Delete
                      </button>
                    </Can>
                  )}
                </td>
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr>
                <td colSpan={6} className="table-empty">
                  No groups match.
                </td>
              </tr>
            )}
            {loading && !data && (
              <tr>
                <td colSpan={6} className="table-empty">
                  Loading...
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {data && <Pagination page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />}

      {dialog.kind === "form" && <GroupFormDialog group={dialog.group} onClose={() => setDialog({ kind: "none" })} onSaved={closeAndRefresh} />}
      {dialog.kind === "permissions" && <GroupPermissionsDialog group={dialog.group} onClose={() => setDialog({ kind: "none" })} onSaved={refresh} />}
      {dialog.kind === "delete" && <DeleteGroupDialog group={dialog.group} onClose={() => setDialog({ kind: "none" })} onDeleted={closeAndRefresh} />}
    </div>
  );
}
