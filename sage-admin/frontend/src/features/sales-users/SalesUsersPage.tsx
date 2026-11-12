"use client";

import { useCallback, useEffect, useState } from "react";
import { Can } from "@/components/guards/Can";
import { Alert } from "@/components/ui/Form";
import { Pagination } from "@/components/ui/Pagination";
import { usePermission } from "@/hooks/usePermission";
import { getSalesUsers, setSalesUserStatus } from "@/services/salesUserService";
import { ApiError, type PagedResult } from "@/types/api";
import { SALES_ROLE_LABEL, type SalesRole, type SalesUser, type SalesUserQuery } from "@/types/salesUser";
import { Perm } from "@/utils/permissionCodes";
import { ResetPasswordDialog, SalesUserFormDialog } from "./SalesUserDialogs";

type Dialog = { kind: "none" } | { kind: "form"; user?: SalesUser } | { kind: "password"; user: SalesUser };
type SortKey = NonNullable<SalesUserQuery["sortBy"]>;

const formatDate = (iso: string | null) => (iso ? new Date(iso).toLocaleString("en-IN", { dateStyle: "medium", timeStyle: "short" }) : "Never");

/** User Panel accounts: sales persons and sales managers, managed by admins. */
export function SalesUsersPage() {
  const canChangeStatus = usePermission(Perm.SalesUserChangeStatus);
  const [search, setSearch] = useState("");
  const [role, setRole] = useState<"" | SalesRole>("");
  const [status, setStatus] = useState<"" | "Active" | "Inactive">("");
  const [team, setTeam] = useState<SalesUser | null>(null);
  const [sortBy, setSortBy] = useState<SortKey>("name");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<PagedResult<SalesUser> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState<Dialog>({ kind: "none" });
  const [refreshKey, setRefreshKey] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(
        await getSalesUsers({
          page,
          pageSize: 15,
          search: search.trim() || undefined,
          role: role || undefined,
          status: status || undefined,
          managerId: team?.id,
          sortBy,
          sortDirection,
        }),
      );
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load sales users.");
    } finally {
      setLoading(false);
    }
  }, [page, search, role, status, team, sortBy, sortDirection]);

  useEffect(() => {
    const t = setTimeout(load, 250);
    return () => clearTimeout(t);
  }, [load, refreshKey]);

  const refresh = () => setRefreshKey((k) => k + 1);
  const closeAndRefresh = () => {
    setDialog({ kind: "none" });
    refresh();
  };

  /** Activate, deactivate, or (Active on an already active user) clear a sign-in lockout. */
  async function changeStatus(u: SalesUser, next: "Active" | "Inactive") {
    const name = `${u.firstName} ${u.lastName}`;
    const unlocking = next === "Active" && u.status === "Active";
    const question = unlocking
      ? `Unlock ${name}? They can sign in again straight away.`
      : next === "Active"
        ? `Activate ${name}?`
        : `Deactivate ${name}? They will be signed out of the User Panel.`;
    if (!window.confirm(question)) return;
    setError(null);
    try {
      await setSalesUserStatus(u.id, next);
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
  const resetPage = () => setPage(1);

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Sales Users</h1>
      <p className="text-sm text-zinc-500">Accounts for the User Panel. Sales persons report to a sales manager; managers can also sell.</p>

      <div className="flex flex-wrap items-center gap-2">
        <input
          aria-label="Search sales users"
          placeholder="Search name, username, email, mobile"
          className={`${field} w-72`}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            resetPage();
          }}
        />
        <select
          aria-label="Filter by role"
          className={field}
          value={role}
          onChange={(e) => {
            setRole(e.target.value as "" | SalesRole);
            resetPage();
          }}
        >
          <option value="">Any role</option>
          <option value="SalesPerson" className="text-black">Sales persons</option>
          <option value="SalesManager" className="text-black">Sales managers</option>
        </select>
        <select
          aria-label="Filter by status"
          className={field}
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as "" | "Active" | "Inactive");
            resetPage();
          }}
        >
          <option value="">Any status</option>
          <option value="Active" className="text-black">Active</option>
          <option value="Inactive" className="text-black">Inactive</option>
        </select>
        {team && (
          <span className="flex items-center gap-2 rounded bg-zinc-100 px-3 py-2 text-sm dark:bg-zinc-800">
            Team of {team.firstName} {team.lastName}
            <button
              type="button"
              aria-label="Clear team filter"
              onClick={() => {
                setTeam(null);
                resetPage();
              }}
            >
              ×
            </button>
          </span>
        )}
        <div className="flex-1" />
        <Can permission={Perm.SalesUserAdd}>
          <button
            type="button"
            onClick={() => setDialog({ kind: "form" })}
            className="rounded bg-zinc-900 px-3 py-2 text-sm text-white dark:bg-zinc-100 dark:text-zinc-900"
          >
            Add sales user
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
              {sortHeader("role", "Role")}
              {sortHeader("manager", "Manager / Team")}
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
                  <div className="text-xs text-zinc-500">
                    {u.email}
                    {u.mobileNumber && ` · ${u.mobileNumber}`}
                  </div>
                </td>
                <td className="px-3 py-2 font-mono">{u.username}</td>
                <td className="px-3 py-2">{SALES_ROLE_LABEL[u.role]}</td>
                <td className="px-3 py-2">
                  {u.role === "SalesManager" ? (
                    <button
                      type="button"
                      className="underline disabled:no-underline disabled:opacity-60"
                      disabled={u.teamSize === 0}
                      onClick={() => {
                        setTeam(u);
                        setRole("");
                        resetPage();
                      }}
                    >
                      Team: {u.teamSize}
                    </button>
                  ) : (
                    (u.managerName ?? "-")
                  )}
                </td>
                <td className="px-3 py-2">
                  {u.status}
                  {u.isLockedOut && <span className="ml-2 text-xs text-red-600">locked out</span>}
                  {u.mustChangePassword && <div className="text-xs text-zinc-500">must change password</div>}
                </td>
                <td className="px-3 py-2 text-xs">{formatDate(u.lastLoginAtUtc)}</td>
                <td className="space-x-3 whitespace-nowrap px-3 py-2 text-right">
                  <Can permission={Perm.SalesUserEdit}>
                    <button type="button" className="underline" onClick={() => setDialog({ kind: "form", user: u })}>
                      Edit
                    </button>
                  </Can>
                  <Can permission={Perm.SalesUserResetPassword}>
                    <button type="button" className="underline" onClick={() => setDialog({ kind: "password", user: u })}>
                      Reset password
                    </button>
                  </Can>
                  {canChangeStatus && u.status === "Active" && u.isLockedOut && (
                    <button type="button" className="underline" onClick={() => changeStatus(u, "Active")}>
                      Unlock
                    </button>
                  )}
                  {canChangeStatus && (
                    <button
                      type="button"
                      className={`underline ${u.status === "Active" ? "text-red-600" : ""}`}
                      onClick={() => changeStatus(u, u.status === "Active" ? "Inactive" : "Active")}
                    >
                      {u.status === "Active" ? "Deactivate" : "Activate"}
                    </button>
                  )}
                </td>
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr>
                <td colSpan={7} className="px-3 py-6 text-center text-zinc-500">
                  No sales users match. Create a sales manager first, then the sales persons who report to them.
                </td>
              </tr>
            )}
            {loading && !data && (
              <tr>
                <td colSpan={7} className="px-3 py-6 text-center text-zinc-500">
                  Loading...
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {data && <Pagination page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onPage={setPage} />}

      {dialog.kind === "form" && <SalesUserFormDialog user={dialog.user} onClose={() => setDialog({ kind: "none" })} onSaved={closeAndRefresh} />}
      {dialog.kind === "password" && (
        <ResetPasswordDialog
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
