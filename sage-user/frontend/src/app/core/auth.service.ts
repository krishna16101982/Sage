import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ApiClient } from './api-client';
import type { CurrentUser, LoginResponse } from './models/auth';

/** Signed-in sales user and session actions. The session is restored from the refresh cookie when the app starts. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);

  /** The signed-in sales user, or null. */
  readonly user = signal<CurrentUser | null>(null);
  readonly isManager = computed(() => this.user()?.isManager ?? false);

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
    const data = await this.api.post<LoginResponse>('/api/user/auth/login', { username, password });
    this.api.setAccessToken(data.accessToken);
    this.user.set(data.user);
    return data.user;
  }

  async logout(): Promise<void> {
    try {
      await this.api.post<null>('/api/user/auth/logout');
    } finally {
      this.api.setAccessToken(null);
      this.user.set(null);
    }
  }

  /** The server ends every session afterwards, so the user signs in again. */
  async changePassword(currentPassword: string, newPassword: string, confirmPassword: string): Promise<void> {
    await this.api.post<null>('/api/user/auth/change-password', { currentPassword, newPassword, confirmPassword });
    this.api.setAccessToken(null);
    this.user.set(null);
  }
}
