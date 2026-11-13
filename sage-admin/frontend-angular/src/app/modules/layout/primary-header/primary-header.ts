import { Component, DestroyRef, computed, inject, output, signal } from '@angular/core';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { Icon, initials } from '../../../component/icon/icon';
import { AuthService } from '../../../services/auth/auth.service';
import { ThemeService } from '../../../services/theme/theme.service';
import { MODULE_ICONS } from '../primary-navigation/primary-navigation';

/** Top bar: menu button, page title, page search, light/dark switch, signed-in user and Sign out. */
@Component({
  selector: 'app-primary-header',
  imports: [Icon],
  templateUrl: './primary-header.html',
})
export class PrimaryHeader {
  readonly toggleNav = output<void>();
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  protected readonly icons = MODULE_ICONS;

  /** The current route's title without the " - Sage Admin" suffix. */
  readonly pageTitle = signal('');
  readonly initials = computed(() => initials(this.auth.user()?.firstName, this.auth.user()?.lastName));

  // ---- page search: finds sidebar screens by name or category ----
  readonly query = signal('');
  readonly activeIndex = signal(0);
  readonly results = computed(() => {
    const q = this.query().trim().toLowerCase();
    if (!q) return [];
    return (this.auth.user()?.menu ?? []).filter((m) => `${m.name} ${m.category ?? ''}`.toLowerCase().includes(q)).slice(0, 8);
  });

  constructor() {
    const update = () => this.pageTitle.set(this.titleOf(this.router.routerState.snapshot.root));
    update();
    const sub = this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(update);
    inject(DestroyRef).onDestroy(() => sub.unsubscribe());
  }

  private titleOf(route: ActivatedRouteSnapshot): string {
    let title = '';
    for (let r: ActivatedRouteSnapshot | null = route; r; r = r.firstChild) if (r.title) title = r.title;
    return title.replace(/ - Sage .*$/, '');
  }

  onSearch(value: string): void {
    this.query.set(value);
    this.activeIndex.set(0);
  }

  onSearchKey(event: KeyboardEvent): void {
    const count = this.results().length;
    if (event.key === 'ArrowDown' && count) {
      event.preventDefault();
      this.activeIndex.set((this.activeIndex() + 1) % count);
    } else if (event.key === 'ArrowUp' && count) {
      event.preventDefault();
      this.activeIndex.set((this.activeIndex() - 1 + count) % count);
    } else if (event.key === 'Enter' && count) {
      event.preventDefault();
      this.go(this.results()[this.activeIndex()].path, event.target as HTMLInputElement);
    } else if (event.key === 'Escape') {
      this.go(null, event.target as HTMLInputElement);
    }
  }

  go(path: string | null, input?: HTMLInputElement): void {
    this.query.set('');
    if (input) {
      input.value = '';
      input.blur();
    }
    if (path) this.router.navigateByUrl(path);
  }

  async logout(): Promise<void> {
    await this.auth.logout();
    this.router.navigateByUrl('/login');
  }
}
