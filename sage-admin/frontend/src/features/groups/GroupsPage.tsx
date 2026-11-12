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
      <th className="px-3 py-2" aria-sort={active ? (sortDirection === "asc" ? "ascending" : "descending") : "none"}>
        <button
          type="button"
          className="font-semibold"
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

  const field = "rounded border border-zinc-300 bg-transparent px-3 py-2 text-sm dark:border-zinc-700";

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Permission Groups</h1>

      <div className="flex flex-wrap items-center gap-2">
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
          <option value="Active" className="text-black">Active</option>
          <option value="Inactive" className="text-black">Inactive</option>
        </select>
        <div className="flex-1" />
        <Can permission={Perm.GroupAdd}>
          <button
            type="button"
            onClick={() => setDialog({ kind: "form" })}
            className="rounded bg-zinc-900 px-3 py-2 text-sm text-white dark:bg-zinc-100 dark:text-zinc-900"
          >
            Add group
          </button>
        </Can>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <div className="overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-50 dark:bg-zinc-900">
            <tr>
              {sortHeader("name", "Name")}
              {sortHeader("code", "Code")}
              {sortHeader("userCount", "Users")}
              <th className="px-3 py-2">Permissions</th>
              {sortHeader("status", "Status")}
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((g) => (
              <tr key={g.id} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="px-3 py-2">
                  {g.name}
                  {g.description && <div className="text-xs text-zinc-500">{g.description}</div>}
                </td>
                <td className="px-3 py-2 font-mono">{g.code}</td>
                <td className="px-3 py-2">{g.userCount}</td>
                <td className="px-3 py-2">{g.permissionCount}</td>
                <td className="px-3 py-2">
                  {g.status}
                  {g.isSystem && <span className="ml-2 text-xs text-zinc-500">system</span>}
                </td>
                <td className="space-x-3 whitespace-nowrap px-3 py-2 text-right">
                  <button type="button" className="underline" onClick={() => setDialog({ kind: "permissions", group: g })}>
                    Permissions
                  </button>
                  <Can permission={Perm.GroupEdit}>
                    <button type="button" className="underline" onClick={() => setDialog({ kind: "form", group: g })}>
                      Edit
                    </button>
                  </Can>
                  {!g.isSystem && (
                    <Can permission={Perm.GroupDelete}>
                      <button type="button" className="text-red-600 underline" onClick={() => setDialog({ kind: "delete", group: g })}>
                        Delete
                      </button>
                    </Can>
                  )}
                </td>
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-3 py-6 text-center text-zinc-500">
                  No groups match.
                </td>
              </tr>
            )}
            {loading && !data && (
              <tr>
                <td colSpan={6} className="px-3 py-6 text-center text-zinc-500">
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
