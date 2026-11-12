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

  const field = "field field-inline";

  return (
    <div className="space-y-3">
      <div className="toolbar">
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
            <option key={m.id} value={m.id}>
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
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </select>
        <div className="toolbar-spacer" />
        <Can permission={Perm.PermissionAdd}>
          <button type="button" onClick={onAdd} className="btn btn-primary">
            Add permission
          </button>
        </Can>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th>Code</th>
              <th>Name</th>
              <th>Module</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((p) => (
              <tr key={p.id}>
                <td className="code">{p.code}</td>
                <td>
                  {p.name}
                  {p.description && <div className="hint">{p.description}</div>}
                </td>
                <td>{p.moduleName}</td>
                <td>
                  {p.isActive ? "Active" : "Inactive"}
                  {p.isSystem && <span className="badge">system</span>}
                </td>
                <td className="actions">
                  <Can permission={Perm.PermissionEdit}>
                    <button type="button" className="link-button" onClick={() => onEdit(p)}>
                      Edit
                    </button>
                  </Can>
                </td>
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr>
                <td colSpan={5} className="table-empty">
                  No permissions match.
                </td>
              </tr>
            )}
            {loading && !data && (
              <tr>
                <td colSpan={5} className="table-empty">
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
