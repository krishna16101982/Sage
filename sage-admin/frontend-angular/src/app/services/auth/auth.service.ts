import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { GlobalRestService } from '../rest/global-rest-service';
import type { CurrentUser, LoginResponse } from '../../shared/interfaces/auth.interface';

/** Signed-in user and session actions. The session is restored from the refresh cookie when the app starts. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(GlobalRestService);
  private readonly router = inject(Router);

  /** The signed-in user, or null. */
  readonly user = signal<CurrentUser | null>(null);
  readonly isSignedIn = computed(() => this.user() !== null);

  constructor() {
    this.api.onSessionExpired = () => {
      this.user.set(null);
      this.router.navigateByUrl('/login');
    };
  }

  /** Runs once at start-up (see app.config.ts). Never throws: a failed restore just means "signed out". */
  async restore(): Promise<void> {
    try {
      this.user.set((await this.api.refreshSession())?.user ?? null);
    } catch {
      this.user.set(null);
    }
  }

  async login(username: string, password: string): Promise<CurrentUser> {
    const data = await this.api.post<LoginResponse>('/api/admin/auth/login', { username, password });
    this.api.setAccessToken(data.accessToken);
    this.user.set(data.user);
    return data.user;
  }

  async logout(): Promise<void> {
    try {
      await this.api.post<null>('/api/admin/auth/logout');
    } finally {
      this.api.setAccessToken(null);
      this.user.set(null);
    }
  }

  forgotPassword(email: string): Promise<null> {
    return this.api.post<null>('/api/admin/auth/forgot-password', { email });
  }

  resetPassword(token: string, newPassword: string, confirmPassword: string): Promise<null> {
    return this.api.post<null>('/api/admin/auth/reset-password', { token, newPassword, confirmPassword });
  }

  /** The server revokes every session afterwards, so the user signs in again. */
  async changePassword(currentPassword: string, newPassword: string, confirmPassword: string): Promise<void> {
    await this.api.post<null>('/api/admin/auth/change-password', { currentPassword, newPassword, confirmPassword });
    this.api.setAccessToken(null);
    this.user.set(null);
  }
}
