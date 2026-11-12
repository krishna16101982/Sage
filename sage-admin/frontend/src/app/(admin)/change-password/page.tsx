"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { Alert, SubmitButton, TextField } from "@/components/ui/Form";
import { useAuth } from "@/contexts/AuthContext";
import { changePassword } from "@/services/authService";
import { ApiError } from "@/types/api";

export default function ChangePasswordPage() {
  const { user, setUser } = useAuth();
  const router = useRouter();
  const [current, setCurrent] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await changePassword(current, password, confirm);
      setUser(null); // every session was revoked: sign in again with the new password
      router.replace("/login");
    } catch (err) {
      setError(err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="max-w-sm space-y-4">
      <h1 className="text-2xl font-semibold">Change password</h1>
      {user?.mustChangePassword && <Alert kind="success">You must choose a new password before continuing.</Alert>}
      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        <TextField label="Current password" type="password" autoComplete="current-password" required value={current} onChange={(e) => setCurrent(e.target.value)} />
        <TextField label="New password" type="password" autoComplete="new-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        <TextField label="Confirm new password" type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} />
        <p className="text-xs text-zinc-500">At least 10 characters with upper and lower case letters, a digit and a symbol.</p>
        <SubmitButton busy={busy}>Change password</SubmitButton>
      </form>
    </div>
  );
}
