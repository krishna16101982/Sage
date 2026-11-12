"use client";

import { useCallback, useEffect, useState } from "react";
import { Can } from "@/components/guards/Can";
import { Alert } from "@/components/ui/Form";
import { Pagination } from "@/components/ui/Pagination";
import { useAuth } from "@/contexts/AuthContext";
import { usePermission } from "@/hooks/usePermission";
import { getAdminUsers, setAdminUserStatus } from "@/services/adminUserService";
import { ApiError, type PagedResult } from "@/types/api";
import type { AdminUserItem, AdminUserQuery } from "@/types/adminUser";
import { Perm } from "@/utils/permissionCodes";
import { AdminResetPasswordDialog, AdminUserFormDialog } from "./AdminUserDialogs";

type Dialog = { kind: "none" } | { kind: "form"; user?: AdminUserItem } | { kind: "password"; user: AdminUserItem };
type SortKey = NonNullable<AdminUserQuery["sortBy"]>;

const formatDate = (iso: string | null) => (iso ? new Date(iso).toLocaleString("en-IN", { dateStyle: "medium", timeStyle: "short" }) : "Never");

/** Admin Panel accounts: create, edit (group), activate/deactivate, unlock, temporary password. */
export function AdminUsersPage() {
  const { user: me } = useAuth();
  const canEdit = usePermission(Perm.AdminUserEdit);
  const canChangeStatus = usePermission(Perm.AdminUserChangeStatus);
  const canReset = usePermission(Perm.AdminUserResetPassword);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<"" | "Active" | "Inactive">("");
  const [sortBy, setSortBy] = useState<SortKey>("name");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<AdminUserItem> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState<Dialog>({ kind: "none" });
  const [refreshKey, setRefreshKey] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await getAdminUsers({ page, pageSize: 15, search: search.trim() || undefined, status: status || undefined, sortBy, sortDirection }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load admin users.");
    } finally {
      setLoading(false);
    }
  }, [page, search, status, sortBy, sortDirection]);

  useEffect(() => {
    const t = setTimeout(load, 250);
    return () => clearTimeout(t);
  }, [load, refreshKey]);

  const refresh = () => setRefreshKey((k) => k + 1);
  const closeAndRefresh = () => {
    setDialog({ kind: "none" });
    refresh();
  };

  /** Only a Super Admin may change a Super Admin account (the API enforces this too). */
  const canManage = (u: AdminUserItem) => !u.isSuperAdmin || !!me?.isSuperAdmin;
  const isMe = (u: AdminUserItem) => u.id === me?.id;

  async function changeStatus(u: AdminUserItem, next: "Active" | "Inactive") {
    const name = `${u.firstName} ${u.lastName}`;
    const unlocking = next === "Active" && u.status === "Active";
    const question = unlocking
      ? `Unlock ${name}? They can sign in again straight away.`
      : next === "Active"
        ? `Activate ${name}?`
        : `Deactivate ${name}? They will be signed out immediately.`;
    if (!window.confirm(question)) return;
    setError(null);
    try {
      await setAdminUserStatus(u.id, next);
      setNotice(`${name} ${unlocking ? "unlocked" : next === "Active" ? "activated" : "deactivated"}.`);
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.messages.join(" ") : "Unable to change the status.");
    }
  }

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
      <h1 className="text-2xl font-semibold">Admin Users</h1>
      <p className="text-sm text-zinc-500">Accounts for this Admin Panel. A user&apos;s permission group decides what they can see and do.</p>

      <div className="flex flex-wrap items-center gap-2">
        <input
          aria-label="Search admin users"
          placeholder="Search name, username, email, mobile"
          className={`${field} w-72`}
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
            setStatus(e.target.value as "" | "Active" | "Inactive");
            setPage(1);
          }}
        >
          <option value="">Any status</option>
          <option value="Active" className="text-black">Active</option>
          <option value="Inactive" className="text-black">Inactive</option>
        </select>
        <div className="flex-1" />
        <Can permission={Perm.AdminUserAdd}>
          <button
            type="button"
            onClick={() => setDialog({ kind: "form" })}
            className="rounded bg-zinc-900 px-3 py-2 text-sm text-white dark:bg-zinc-100 dark:text-zinc-900"
          >
            Add admin user
          </button>
        </Can>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {notice && <Alert kind="success">{notice}</Alert>}

      <div className="overflow-x-auto rounded border border-zinc-200 dark:border-zinc-800">
        <table className="w-full text-left text-sm">
          <thead className="bg-zinc-50 dark:bg-zinc-900">
            <tr>
              {sortHeader("name", "Name")}
              {sortHeader("username", "Username")}
              {sortHeader("group", "Group")}
              {sortHeader("status", "Status")}
              {sortHeader("lastLoginAtUtc", "Last sign-in")}
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((u) => (
              <tr key={u.id} className="border-t border-zinc-200 dark:border-zinc-800">
                <td className="px-3 py-2">
                  {u.firstName} {u.lastName}
                  {isMe(u) && <span className="ml-2 text-xs text-zinc-500">(you)</span>}
                  <div className="text-xs text-zinc-500">
                    {u.email}
                    {u.mobileNumber && ` · ${u.mobileNumber}`}
                  </div>
                </td>
                <td className="px-3 py-2 font-mono">{u.username}</td>
                <td className="px-3 py-2">
                  {u.isSuperAdmin ? (
                    <span className="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-900 dark:bg-amber-900 dark:text-amber-100">Super Admin</span>
                  ) : u.groupName ? (
                    <>
                      {u.groupName}
                      {!u.groupIsActive && <span className="ml-2 text-xs text-red-600">group inactive</span>}
                    </>
                  ) : (
                    <span className="text-zinc-500">No group</span>
                  )}
                </td>
                <td className="px-3 py-2">
                  {u.status}
                  {u.isLockedOut && <span className="ml-2 text-xs text-red-600">locked out</span>}
                  {u.mustChangePassword && <div className="text-xs text-zinc-500">must change password</div>}
                </td>
                <td className="px-3 py-2 text-xs">{formatDate(u.lastLoginAtUtc)}</td>
                <td className="space-x-3 whitespace-nowrap px-3 py-2 text-right">
                  {canManage(u) && (
                    <>
                      {canEdit && (
                        <button type="button" className="underline" onClick={() => setDialog({ kind: "form", user: u })}>
                          Edit
                        </button>
                      )}
                      {canReset && !isMe(u) && (
                        <button type="button" className="underline" onClick={() => setDialog({ kind: "password", user: u })}>
                          Reset password
                        </button>
                      )}
                      {canChangeStatus && u.status === "Active" && u.isLockedOut && (
                        <button type="button" className="underline" onClick={() => changeStatus(u, "Active")}>
                          Unlock
                        </button>
                      )}
                      {canChangeStatus && !isMe(u) && (
                        <button
                          type="button"
                          className={`underline ${u.status === "Active" ? "text-red-600" : ""}`}
                          onClick={() => changeStatus(u, u.status === "Active" ? "Inactive" : "Active")}
                        >
                          {u.status === "Active" ? "Deactivate" : "Activate"}
                        </button>
                      )}
                    </>
                  )}
                </td>
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr>
                <td colSpan={6} className="px-3 py-6 text-center text-zinc-500">
                  No admin users match.
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

      {dialog.kind === "form" && <AdminUserFormDialog user={dialog.user} onClose={() => setDialog({ kind: "none" })} onSaved={closeAndRefresh} />}
      {dialog.kind === "password" && (
        <AdminResetPasswordDialog
          user={dialog.user}
          onClose={() => setDialog({ kind: "none" })}
          onDone={(message) => {
            setNotice(message);
            closeAndRefresh();
          }}
        />
      )}
    </div>
  );
}
