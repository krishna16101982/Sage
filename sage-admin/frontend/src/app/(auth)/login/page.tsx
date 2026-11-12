"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { Alert, AuthCard, SubmitButton, TextField } from "@/components/ui/Form";
import { useAuth } from "@/contexts/AuthContext";
import { ApiError } from "@/types/api";

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const user = await login(username, password);
      router.replace(user.mustChangePassword ? "/change-password" : "/dashboard");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to sign in. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <AuthCard title="Sage Admin">
      <form onSubmit={onSubmit} className="space-y-4" aria-label="Sign in">
        {error && <Alert kind="error">{error}</Alert>}
        <TextField label="Username or email" name="username" autoComplete="username" required value={username} onChange={(e) => setUsername(e.target.value)} />
        <TextField label="Password" name="password" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        <SubmitButton busy={busy}>Sign in</SubmitButton>
      </form>
      <Link href="/forgot-password" className="block text-sm underline">
        Forgot password?
      </Link>
    </AuthCard>
  );
}
