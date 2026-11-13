import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth/auth.service';
import { SalesService } from '../../services/sales/sales.service';
import type { SalesPage } from '../../shared/interfaces/sale.interface';
import { firstOfMonth } from '../../shared/utils/dates';
import { formatInr } from '../../shared/utils/format';

/** Landing page after sign-in: this month's figures (own, and the team's for a manager) and quick links. */
@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.html',
})
export class Home implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly sales = inject(SalesService);
  protected readonly inr = formatInr;
  readonly mine = signal<SalesPage | null>(null);
  readonly team = signal<SalesPage | null>(null);

  ngOnInit(): void {
    // Only the totals are needed: one row per call is enough.
    const query = { soldFrom: firstOfMonth(), status: 'Active' as const, pageSize: 1 };
    this.sales.list({ ...query, scope: 'mine' }).then((p) => this.mine.set(p), () => undefined);
    if (this.auth.isManager()) this.sales.list({ ...query, scope: 'team' }).then((p) => this.team.set(p), () => undefined);
  }
}
