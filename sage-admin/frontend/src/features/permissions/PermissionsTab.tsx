"use client";

import { useCallback, useEffect, useState } from "react";
import { Can } from "@/components/guards/Can";
import { Alert } from "@/components/ui/Form";
import { Pagination } from "@/components/ui/Pagination";
import { getPermissions } from "@/services/permissionService";
import { ApiError, type PagedResult } from "@/types/api";
import type { ModuleItemDto, PermissionItem } from "@/types/permission";
import { Perm } from "@/utils/permissionCodes";

export function PermissionsTab({
  modules,
  refreshKey,
  onAdd,
  onEdit,
}: {
  modules: ModuleItemDto[];
  refreshKey: number;
  onAdd: () => void;
  onEdit: (p: PermissionItem) => void;
}) {
  const [search, setSearch] = useState("");
  const [moduleId, setModuleId] = useState<number | "">("");
  const [status, setStatus] = useState<"" | "true" | "false">("");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<PermissionItem> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(
        await getPermissions({
          page,
          pageSize: 15,
          search: search.trim() || undefined,
          moduleId: moduleId === "" ? undefined : moduleId,
          isActive: status === "" ? undefined : status === "true",
          sortBy: "module",
        }),
      );
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load permissions.");
    } finally {
      setLoading(false);
    }
  }, [page, search, moduleId, status]);

  // Debounce typing in the search box; reload when filters, page or the parent's refresh key change.
  useEffect(() => {
    const t = setTimeout(load, 250);
    return () => clearTimeout(t);
  }, [load, refreshKey]);

  const field = "rounded border border-zinc-300 bg-transparent px-3 py-2 text-sm dark:border-zinc-700";

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <input
          aria-label="Search permissions"
          placeholder="Search code, name or module"
          className={`${field} w-64`}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        <select
          aria-label="Filter by module"
          className={field}
          value={moduleId}
          onChange={(e) => {
            setModuleId(e.target.value === "" ? "" : Number(e.target.value));
            setPage(1);
          }}
        >
          <option value="">All modules</option>
          {modules.map((m) => (
            <option key={m.id} value={m.id} className="text-black">
              {m.name}
            </option>
          ))}
        </select>
        <select
          aria-label="Filter by status"
          className={field}
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as "" | "true" | "false");
            setPage(1);
          }}
        >
          <option value="">Any status</option>
          <option value="true" className="text-black">Active</option>
          <option value="false" className="text-black">Inactive</option>
        </select>
        <div className="flex-1" />
        <Can permission={Perm.PermissionAdd}>
          <button type="button" onClick={onAdd} className="rounded bg-zinc-900 px-3 py-2 text-sm text-white dark:bg-zinc-100 dark:text-zinc-900">
            Add permission
          </button>
        </Can>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <div className="overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-50 dark:bg-zinc-900">
            <tr>
              <th className="px-3 py-2">Code</th>
              <th className="px-3 py-2">Name</th>
              <th className="px-3 py-2">Module</th>
              <th className="px-3 py-2">Status</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((p) => (
              <tr key={p.id} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="px-3 py-2 font-mono">{p.code}</td>
                <td className="px-3 py-2">
                  {p.name}
                  {p.description && <div className="text-xs text-zinc-500">{p.description}</div>}
                </td>
                <td className="px-3 py-2">{p.moduleName}</td>
                <td className="px-3 py-2">
                  {p.isActive ? "Active" : "Inactive"}
                  {p.isSystem && <span className="ml-2 text-xs text-zinc-500">system</span>}
                </td>
                <td className="px-3 py-2 text-right">
                  <Can permission={Perm.PermissionEdit}>
                    <button type="button" className="underline" onClick={() => onEdit(p)}>
                      Edit
                    </button>
                  </Can>
                </td>
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr>
                <td colSpan={5} className="px-3 py-6 text-center text-zinc-500">
                  No permissions match.
                </td>
              </tr>
            )}
            {loading && !data && (
              <tr>
                <td colSpan={5} className="px-3 py-6 text-center text-zinc-500">
                  Loading...
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {data && <Pagination page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />}
    </div>
  );
}
