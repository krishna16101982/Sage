"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState, type FormEvent } from "react";
import { Alert, AuthCard, SubmitButton, TextField } from "@/components/ui/Form";
import { resetPassword } from "@/services/authService";
import { ApiError } from "@/types/api";

function ResetForm() {
  const router = useRouter();
  const token = useSearchParams().get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await resetPassword(token, password, confirm);
      router.replace("/login");
    } catch (err) {
      setError(err instanceof ApiError ? err.messages.join(" ") : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  if (!token) return <Alert kind="error">This reset link is missing its token. Request a new one.</Alert>;

  return (
    <form onSubmit={onSubmit} className="space-y-4">
      {error && <Alert kind="error">{error}</Alert>}
      <TextField label="New password" type="password" autoComplete="new-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
      <TextField label="Confirm password" type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} />
      <p className="text-xs text-zinc-500">At least 10 characters with upper and lower case letters, a digit and a symbol.</p>
      <SubmitButton busy={busy}>Reset password</SubmitButton>
    </form>
  );
}

export default function ResetPasswordPage() {
  return (
    <AuthCard title="Reset password">
      <Suspense>
        <ResetForm />
      </Suspense>
      <Link href="/login" className="block text-sm underline">
        Back to sign in
      </Link>
    </AuthCard>
  );
}
