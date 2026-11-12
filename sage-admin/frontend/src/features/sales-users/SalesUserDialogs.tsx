"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Alert, SubmitButton, TextField } from "@/components/ui/Form";
import { Modal } from "@/components/ui/Modal";
import { createSalesUser, getSalesManagers, resetSalesUserPassword, updateSalesUser } from "@/services/salesUserService";
import { ApiError } from "@/types/api";
import { SALES_ROLE_LABEL, type SalesManagerOption, type SalesRole, type SalesUser } from "@/types/salesUser";

const field = "mt-1 w-full rounded border border-zinc-300 bg-transparent px-3 py-2 dark:border-zinc-700";
const errorText = (err: unknown) => (err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
const PASSWORD_HINT = "At least 10 characters with upper and lower case letters, a digit and a symbol.";

/** Create (no `user`) or edit a sales user. A sales person needs an active sales manager; a manager has none. */
export function SalesUserFormDialog({ user, onClose, onSaved }: { user?: SalesUser; onClose: () => void; onSaved: () => void }) {
  const editing = !!user;
  const [firstName, setFirstName] = useState(user?.firstName ?? "");
  const [lastName, setLastName] = useState(user?.lastName ?? "");
  const [username, setUsername] = useState(user?.username ?? "");
  const [email, setEmail] = useState(user?.email ?? "");
  const [mobileNumber, setMobileNumber] = useState(user?.mobileNumber ?? "");
  const [role, setRole] = useState<SalesRole>(user?.role ?? "SalesPerson");
  const [managerId, setManagerId] = useState(user?.managerId ?? "");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [managers, setManagers] = useState<SalesManagerOption[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getSalesManagers()
      .then((m) => !cancelled && setManagers(m))
      .catch((err) => !cancelled && setError(errorText(err)));
    return () => {
      cancelled = true;
    };
  }, []);

  // A user cannot report to themselves; keep the current (possibly now inactive) manager visible so it is not silently changed.
  const managerOptions = (managers ?? []).filter((m) => m.id !== user?.id);
  const currentMissing = user?.managerId && !managerOptions.some((m) => m.id === user.managerId);
  const demotingManagerWithTeam = editing && user?.role === "SalesManager" && role === "SalesPerson" && user.teamSize > 0;

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const data = {
        firstName,
        lastName,
        email,
        mobileNumber: mobileNumber || undefined,
        role,
        managerId: role === "SalesPerson" && managerId ? managerId : undefined,
      };
      if (user) await updateSalesUser(user.id, data);
      else await createSalesUser({ ...data, username, password, confirmPassword });
      onSaved();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title={editing ? "Edit sales user" : "Add sales user"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <div className="grid grid-cols-2 gap-3">
          <TextField label="First name" required value={firstName} onChange={(e) => setFirstName(e.target.value)} maxLength={100} />
          <TextField label="Last name" required value={lastName} onChange={(e) => setLastName(e.target.value)} maxLength={100} />
        </div>
        <TextField label="Username" required disabled={editing} value={username} onChange={(e) => setUsername(e.target.value)} maxLength={50} autoComplete="off" />
        <TextField label="Email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} maxLength={256} />
        <TextField label="Mobile" value={mobileNumber} onChange={(e) => setMobileNumber(e.target.value)} maxLength={20} placeholder="+91 98765 43210" />

        <fieldset className="space-y-2 text-sm">
          <legend>Role</legend>
          <div className="flex gap-4">
            {(Object.keys(SALES_ROLE_LABEL) as SalesRole[]).map((r) => (
              <label key={r} className="flex items-center gap-2">
                <input type="radio" name="role" value={r} checked={role === r} onChange={() => setRole(r)} />
                {SALES_ROLE_LABEL[r]}
              </label>
            ))}
          </div>
          <p className="text-xs text-zinc-500">A sales manager can also sell, and sees their team&apos;s sales.</p>
        </fieldset>
        {demotingManagerWithTeam && (
          <Alert kind="error">This manager has {user?.teamSize} sales person(s). Move them to another manager before changing the role.</Alert>
        )}

        {role === "SalesPerson" && (
          <label className="block text-sm">
            Sales manager
            <select required value={managerId} onChange={(e) => setManagerId(e.target.value)} className={field}>
              <option value="" className="text-black">
                {managers === null ? "Loading..." : managerOptions.length === 0 ? "No active sales managers yet" : "Select a manager"}
              </option>
              {currentMissing && user?.managerId && (
                <option value={user.managerId} className="text-black">
                  {user.managerName} (inactive)
                </option>
              )}
              {managerOptions.map((m) => (
                <option key={m.id} value={m.id} className="text-black">
                  {m.name} ({m.username}) · team {m.teamSize}
                </option>
              ))}
            </select>
          </label>
        )}

        {!editing && (
          <>
            <TextField label="Temporary password" type="password" required value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" />
            <TextField label="Confirm password" type="password" required value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
            <p className="text-xs text-zinc-500">{PASSWORD_HINT} The user must change it at first sign-in to the User Panel.</p>
          </>
        )}
        <SubmitButton busy={busy}>{editing ? "Save changes" : "Create sales user"}</SubmitButton>
      </form>
    </Modal>
  );
}

/** Admin sets a temporary password; the user must change it at next sign-in. */
export function ResetPasswordDialog({ user, onClose, onDone }: { user: SalesUser; onClose: () => void; onDone: (message: string) => void }) {
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await resetSalesUserPassword(user.id, password, confirmPassword);
      onDone(`Temporary password set for ${user.firstName} ${user.lastName}. They must change it at next sign-in.`);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title={`Reset password: ${user.firstName} ${user.lastName}`} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <TextField label="Temporary password" type="password" required value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" />
        <TextField label="Confirm password" type="password" required value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
        <p className="text-xs text-zinc-500">{PASSWORD_HINT} Their current sessions end and any lockout is cleared.</p>
        <SubmitButton busy={busy}>Set temporary password</SubmitButton>
      </form>
    </Modal>
  );
}
