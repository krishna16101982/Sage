import { Injectable, signal } from '@angular/core';

type ThemeMode = 'light' | 'dark';

/**
 * Light / dark switch in the top bar. Sets <html data-theme="light|dark"> (theme.css has the colours) and remembers the
 * choice in this browser. Until the user picks one, the device's own setting is used. Same file in both Angular apps.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private static readonly storageKey = 'sage-theme';
  readonly mode = signal<ThemeMode>(this.initial());

  constructor() {
    this.apply(this.mode(), false);
  }

  toggle(): void {
    this.apply(this.mode() === 'dark' ? 'light' : 'dark', true);
  }

  private apply(mode: ThemeMode, remember: boolean): void {
    this.mode.set(mode);
    if (remember) {
      document.documentElement.dataset['theme'] = mode;
      try {
        localStorage.setItem(ThemeService.storageKey, mode);
      } catch {
        // Private mode or blocked storage: the choice just lasts for this visit.
      }
    }
  }

  private initial(): ThemeMode {
    let saved: string | null = null;
    try {
      saved = localStorage.getItem(ThemeService.storageKey);
    } catch {
      saved = null;
    }
    if (saved === 'light' || saved === 'dark') {
      document.documentElement.dataset['theme'] = saved;
      return saved;
    }
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
