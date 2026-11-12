import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiError, type ApiResponse } from '../../shared/interfaces/api.interface';
import type { LoginResponse } from '../../shared/interfaces/auth.interface';

/**
 * Calls the User Panel API and unwraps its { status, messageType, message, data } envelope.
 * The access token lives in memory only (never localStorage); the refresh token is an HttpOnly cookie,
 * so every call is sent withCredentials. A 401 triggers one silent refresh and a retry.
 */
@Injectable({ providedIn: 'root' })
export class GlobalRestService {
  private readonly http = inject(HttpClient);
  private accessToken: string | null = null;
  private refreshing: Promise<LoginResponse | null> | null = null;

  /** Called when the session cannot be refreshed any more (set by AuthService). */
  onSessionExpired: (() => void) | null = null;

  setAccessToken(token: string | null): void {
    this.accessToken = token;
  }

  get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path);
  }

  post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('POST', path, body);
  }

  put<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>('PUT', path, body);
  }

  delete<T>(path: string): Promise<T> {
    return this.request<T>('DELETE', path);
  }

  /** Single-flight refresh: parallel 401s share one refresh call. */
  refreshSession(): Promise<LoginResponse | null> {
    if (!this.refreshing) {
      this.refreshing = this.send<LoginResponse>('POST', '/api/user/auth/refresh-token')
        .then((r) => {
          this.accessToken = r.ok && r.body?.status && r.body.data ? r.body.data.accessToken : null;
          return this.accessToken ? r.body!.data : null;
        })
        .finally(() => (this.refreshing = null));
    }
    return this.refreshing;
  }

  private async request<T>(method: string, path: string, body?: unknown): Promise<T> {
    let r = await this.send<T>(method, path, body);

    // Expired access token: refresh once and retry (not for login/refresh/forgot/reset themselves).
    if (r.status === 401 && !this.isAuthPath(path)) {
      if (await this.refreshSession()) r = await this.send<T>(method, path, body);
      else this.onSessionExpired?.();
    }

    if (!r.ok || !r.body?.status) {
      throw new ApiError(r.status, r.body?.messageType ?? 'SYSTEM_ERROR', r.body?.message ?? ['Request failed']);
    }
    return r.body.data as T;
  }

  private isAuthPath(path: string): boolean {
    return path.startsWith('/api/user/auth/') && !path.endsWith('/me') && !path.endsWith('/change-password');
  }

  /** One HTTP call. Never throws for HTTP errors: returns the status and the parsed envelope (if any). */
  private async send<T>(method: string, path: string, body?: unknown): Promise<{ ok: boolean; status: number; body: ApiResponse<T> | null }> {
    let headers = new HttpHeaders({ Accept: 'application/json' });
    if (this.accessToken) headers = headers.set('Authorization', `Bearer ${this.accessToken}`);
    try {
      const res = await firstValueFrom(
        this.http.request<ApiResponse<T>>(method, `${environment.apiBaseUrl}${path}`, { body, headers, withCredentials: true, observe: 'response' }),
      );
      return { ok: true, status: res.status, body: res.body };
    } catch (err) {
      if (err instanceof HttpErrorResponse) {
        const envelope = err.error && typeof err.error === 'object' && 'status' in err.error ? (err.error as ApiResponse<T>) : null;
        return { ok: false, status: err.status, body: envelope };
      }
      throw err;
    }
  }
}

/** Builds "?a=1&b=x" from an object, skipping empty values. */
export function toQuery(q: object): string {
  const p = new URLSearchParams();
  Object.entries(q).forEach(([k, v]) => {
    if (v !== undefined && v !== '' && v !== null) p.set(k, String(v));
  });
  const s = p.toString();
  return s ? `?${s}` : '';
}

/** The message to show for a failed call. */
export function errorText(err: unknown, fallback = 'Something went wrong. Please try again.'): string {
  return err instanceof ApiError ? err.messages.join(' ') : fallback;
}
