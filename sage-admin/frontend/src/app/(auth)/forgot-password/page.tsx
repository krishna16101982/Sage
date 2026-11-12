"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { Alert, AuthCard, SubmitButton, TextField } from "@/components/ui/Form";
import { forgotPassword } from "@/services/authService";
import { ApiError } from "@/types/api";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setMessage(null);
    setBusy(true);
    try {
      await forgotPassword(email);
      setMessage("If the email belongs to an active account, a reset link has been sent.");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthCard title="Forgot password">
      <form onSubmit={onSubmit} className="space-y-4">
        {message && <Alert kind="success">{message}</Alert>}
        {error && <Alert kind="error">{error}</Alert>}
        <TextField label="Email" name="email" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        <SubmitButton busy={busy}>Send reset link</SubmitButton>
      </form>
      <Link href="/login" className="block text-sm underline">
        Back to sign in
      </Link>
    </AuthCard>
  );
}
