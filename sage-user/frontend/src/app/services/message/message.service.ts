import { Injectable, signal } from '@angular/core';

/**
 * Carries a success message from one page to the next, e.g. "Admin user created." shown on the list after the Add page
 * navigates back. The list reads it once with take().
 */
@Injectable({ providedIn: 'root' })
export class MessageService {
  private readonly pending = signal<string | null>(null);

  success(message: string): void {
    this.pending.set(message);
  }

  /** Returns the pending message (if any) and clears it. */
  take(): string | null {
    const m = this.pending();
    this.pending.set(null);
    return m;
  }
}
