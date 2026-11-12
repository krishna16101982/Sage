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
  const resetPage = () => setPage(1);

  return (
    <div className="space-y-4">
      <h1 className="page-title">Sales Users</h1>
      <p className="text-sm text-muted">Accounts for the User Panel. Sales persons report to a sales manager; managers can also sell.</p>

      <div className="toolbar">
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
          <option value="SalesPerson">Sales persons</option>
          <option value="SalesManager">Sales managers</option>
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
          <option value="Active">Active</option>
          <option value="Inactive">Inactive</option>
        </select>
        {team && (
          <span className="chip">
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
        <div className="toolbar-spacer" />
        <Can permission={Perm.SalesUserAdd}>
          <button
            type="button"
            onClick={() => setDialog({ kind: "form" })}
            className="btn btn-primary"
          >
            Add sales user
          </button>
        </Can>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {notice && <Alert kind="success">{notice}</Alert>}

      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              {sortHeader("name", "Name")}
              {sortHeader("username", "Username")}
              {sortHeader("role", "Role")}
              {sortHeader("manager", "Manager / Team")}
              {sortHeader("status", "Status")}
              {sortHeader("lastLoginAtUtc", "Last sign-in")}
              <th />
            </tr>
          </thead>
          <tbody>
            {data?.items.map((u) => (
              <tr key={u.id}>
                <td>
                  {u.firstName} {u.lastName}
                  <div className="hint">
                    {u.email}
                    {u.mobileNumber && ` · ${u.mobileNumber}`}
                  </div>
                </td>
                <td className="code">{u.username}</td>
                <td>{SALES_ROLE_LABEL[u.role]}</td>
                <td>
                  {u.role === "SalesManager" ? (
                    <button
                      type="button"
                      className="link-button"
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
                <td>
                  {u.status}
                  {u.isLockedOut && <span className="badge badge-danger">locked out</span>}
                  {u.mustChangePassword && <div className="hint">must change password</div>}
                </td>
                <td className="date">{formatDate(u.lastLoginAtUtc)}</td>
                <td className="actions">
                  <Can permission={Perm.SalesUserEdit}>
                    <button type="button" className="link-button" onClick={() => setDialog({ kind: "form", user: u })}>
                      Edit
                    </button>
                  </Can>
                  <Can permission={Perm.SalesUserResetPassword}>
                    <button type="button" className="link-button" onClick={() => setDialog({ kind: "password", user: u })}>
                      Reset password
                    </button>
                  </Can>
                  {canChangeStatus && u.status === "Active" && u.isLockedOut && (
                    <button type="button" className="link-button" onClick={() => changeStatus(u, "Active")}>
                      Unlock
                    </button>
                  )}
                  {canChangeStatus && (
                    <button
                      type="button"
                      className={`link-button ${u.status === "Active" ? "danger" : ""}`}
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
                <td colSpan={7} className="table-empty">
                  No sales users match. Create a sales manager first, then the sales persons who report to them.
                </td>
              </tr>
            )}
            {loading && !data && (
              <tr>
                <td colSpan={7} className="table-empty">
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
