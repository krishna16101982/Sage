import { apiGet, apiPost, refreshSession, setAccessToken } from "./apiClient";
import type { CurrentUser, LoginResponse } from "@/types/auth";

export async function login(username: string, password: string): Promise<CurrentUser> {
  const data = await apiPost<LoginResponse>("/api/admin/auth/login", { username, password });
  setAccessToken(data.accessToken);
  return data.user;
}

/** Restore a session on page load from the refresh-token cookie. Returns null when signed out. */
export async function restoreSession(): Promise<CurrentUser | null> {
  const data = await refreshSession();
  return data?.user ?? null;
}

export async function logout(): Promise<void> {
  try {
    await apiPost<null>("/api/admin/auth/logout");
  } finally {
    setAccessToken(null);
  }
}

export const getMe = () => apiGet<CurrentUser>("/api/admin/auth/me");

export const forgotPassword = (email: string) => apiPost<null>("/api/admin/auth/forgot-password", { email });

export const resetPassword = (token: string, newPassword: string, confirmPassword: string) =>
  apiPost<null>("/api/admin/auth/reset-password", { token, newPassword, confirmPassword });

export async function changePassword(currentPassword: string, newPassword: string, confirmPassword: string): Promise<void> {
  await apiPost<null>("/api/admin/auth/change-password", { currentPassword, newPassword, confirmPassword });
  setAccessToken(null); // the server revoked every session
}
