import { ApiError, type ApiResponse } from "@/types/api";
import type { LoginResponse } from "@/types/auth";

export const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

/** Access token lives in memory only (never localStorage). The refresh token is an HttpOnly cookie. */
let accessToken: string | null = null;
export const setAccessToken = (token: string | null) => {
  accessToken = token;
};
export const getAccessToken = () => accessToken;

/** Called when a refresh fails for good (session over). The AuthContext registers a handler. */
let onSessionExpired: (() => void) | null = null;
export const setSessionExpiredHandler = (handler: (() => void) | null) => {
  onSessionExpired = handler;
};

async function send<T>(path: string, init: RequestInit): Promise<{ res: Response; body: ApiResponse<T> | null }> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (init.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  if (accessToken) headers.set("Authorization", `Bearer ${accessToken}`);

  const res = await fetch(`${API_BASE_URL}${path}`, { ...init, headers, credentials: "include" });
  let body: ApiResponse<T> | null = null;
  try {
    body = (await res.json()) as ApiResponse<T>;
  } catch {
    /* non-JSON response */
  }
  return { res, body };
}

/** Single-flight refresh so parallel 401s trigger one refresh call. */
let refreshing: Promise<LoginResponse | null> | null = null;
export function refreshSession(): Promise<LoginResponse | null> {
  if (!refreshing) {
    refreshing = (async () => {
      const { res, body } = await send<LoginResponse>("/api/admin/auth/refresh-token", { method: "POST" });
      if (res.ok && body?.status && body.data) {
        accessToken = body.data.accessToken;
        return body.data;
      }
      accessToken = null;
      return null;
    })().finally(() => {
      refreshing = null;
    });
  }
  return refreshing;
}

const isAuthPath = (path: string) => path.startsWith("/api/admin/auth/") && !path.endsWith("/me") && !path.endsWith("/change-password");

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  let { res, body } = await send<T>(path, init);

  // Expired access token: refresh once and retry (not for login/refresh/forgot/reset themselves).
  if (res.status === 401 && !isAuthPath(path)) {
    const session = await refreshSession();
    if (session) {
      ({ res, body } = await send<T>(path, init));
    } else {
      onSessionExpired?.();
    }
  }

  if (!res.ok || !body?.status) {
    throw new ApiError(res.status, body?.messageType ?? "SYSTEM_ERROR", body?.message ?? [res.statusText]);
  }
  return body.data as T;
}

export const apiGet = <T,>(path: string) => apiRequest<T>(path);
export const apiPost = <T,>(path: string, data?: unknown) =>
  apiRequest<T>(path, { method: "POST", body: data === undefined ? undefined : JSON.stringify(data) });
