"use client";

import { useEffect, useState, type FormEvent } from "react";
import { Alert, SubmitButton, TextField } from "@/components/ui/Form";
import { Modal } from "@/components/ui/Modal";
import { createAdminUser, getGroupOptions, resetAdminUserPassword, updateAdminUser } from "@/services/adminUserService";
import { ApiError } from "@/types/api";
import type { AdminUserItem, GroupOption } from "@/types/adminUser";

const field = "field";
const errorText = (err: unknown) => (err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
const PASSWORD_HINT = "At least 10 characters with upper and lower case letters, a digit and a symbol.";

/** Create (no `user`) or edit an admin user. The group decides their permissions; no group means no access yet. */
export function AdminUserFormDialog({ user, onClose, onSaved }: { user?: AdminUserItem; onClose: () => void; onSaved: () => void }) {
  const editing = !!user;
  const [firstName, setFirstName] = useState(user?.firstName ?? "");
  const [lastName, setLastName] = useState(user?.lastName ?? "");
  const [username, setUsername] = useState(user?.username ?? "");
  const [email, setEmail] = useState(user?.email ?? "");
  const [mobileNumber, setMobileNumber] = useState(user?.mobileNumber ?? "");
  const [groupId, setGroupId] = useState(user?.groupId ?? "");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [groups, setGroups] = useState<GroupOption[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getGroupOptions()
      .then((g) => !cancelled && setGroups(g))
      .catch((err) => !cancelled && setError(errorText(err)));
    return () => {
      cancelled = true;
    };
  }, []);

  // Keep the user's current group selectable even if it has since been deactivated, so it is not silently dropped.
  const currentInactive = user?.groupId && !user.groupIsActive && !(groups ?? []).some((g) => g.id === user.groupId);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const data = { firstName, lastName, email, mobileNumber: mobileNumber || undefined, groupId: groupId || undefined };
      if (user) await updateAdminUser(user.id, data);
      else await createAdminUser({ ...data, username, password, confirmPassword });
      onSaved();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title={editing ? "Edit admin user" : "Add admin user"} onClose={onClose}>
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <div className="form-grid-2">
          <TextField label="First name" required value={firstName} onChange={(e) => setFirstName(e.target.value)} maxLength={100} />
          <TextField label="Last name" required value={lastName} onChange={(e) => setLastName(e.target.value)} maxLength={100} />
        </div>
        <TextField label="Username" required disabled={editing} value={username} onChange={(e) => setUsername(e.target.value)} maxLength={50} autoComplete="off" />
        <TextField label="Email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} maxLength={256} />
        <TextField label="Mobile" value={mobileNumber} onChange={(e) => setMobileNumber(e.target.value)} maxLength={20} placeholder="+91 98765 43210" />
        {user?.isSuperAdmin ? (
          <p className="text-sm text-muted">Super Admin: has every permission regardless of group.</p>
        ) : (
          <label className="field-label">
            Permission group
            <select value={groupId} onChange={(e) => setGroupId(e.target.value)} className={field}>
              <option value="">
                {groups === null ? "Loading..." : "No group (no access yet)"}
              </option>
              {currentInactive && user?.groupId && (
                <option value={user.groupId}>
                  {user.groupName} (inactive)
                </option>
              )}
              {(groups ?? []).map((g) => (
                <option key={g.id} value={g.id}>
                  {g.name}
                </option>
              ))}
            </select>
            <span className="hint mt-1 block">The group decides which screens and actions this user can use.</span>
          </label>
        )}
        {!editing && (
          <>
            <TextField label="Temporary password" type="password" required value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" />
            <TextField label="Confirm password" type="password" required value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} autoComplete="new-password" />
            <p className="hint">{PASSWORD_HINT} The user must change it at first sign-in.</p>
          </>
        )}
        <SubmitButton busy={busy}>{editing ? "Save changes" : "Create admin user"}</SubmitButton>
      </form>
    </Modal>
  );
}

/** Set a temporary password for another admin user; they must change it at next sign-in. */
export function AdminResetPasswordDialog({ user, onClose, onDone }: { user: AdminUserItem; onClose: () => void; onDone: (message: string) => void }) {
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await resetAdminUserPassword(user.id, password, confirmPassword);
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
        <p className="hint">{PASSWORD_HINT} Their current sessions end and any lockout is cleared.</p>
        <SubmitButton busy={busy}>Set temporary password</SubmitButton>
      </form>
    </Modal>
  );
}
