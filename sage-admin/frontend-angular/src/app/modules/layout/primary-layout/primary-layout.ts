import { Component, DestroyRef, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { ThemeService } from '../../../services/theme/theme.service';
import { PrimaryHeader } from '../primary-header/primary-header';
import { PrimaryNavigation } from '../primary-navigation/primary-navigation';

/**
 * Signed-in frame: dark sidebar on the left, top bar, the current page below it.
 * The top bar's menu button collapses the sidebar to icons (wide screens) or slides it in (phones).
 */
@Component({
  selector: 'app-primary-layout',
  imports: [RouterOutlet, PrimaryHeader, PrimaryNavigation],
  templateUrl: './primary-layout.html',
})
export class PrimaryLayout {
  /** Phones: sidebar shown over the page. */
  readonly navOpen = signal(false);
  /** Wide screens: sidebar shows icons only. */
  readonly navCollapsed = signal(false);

  constructor() {
    inject(ThemeService); // applies the saved light / dark choice
    const sub = inject(Router).events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => this.navOpen.set(false));
    inject(DestroyRef).onDestroy(() => sub.unsubscribe());
  }

  toggleNav(): void {
    if (window.matchMedia('(max-width: 1023px)').matches) this.navOpen.update((v) => !v);
    else this.navCollapsed.update((v) => !v);
  }
}
