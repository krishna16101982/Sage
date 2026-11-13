import { Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { Sale } from '../../../shared/interfaces/sale.interface';
import { formatDate } from '../../../shared/utils/dates';
import { formatDateTime, formatDuration, formatInr } from '../../../shared/utils/format';

/** /sales/:id — one sale, read-only (sales are recorded and cancelled in the User Panel). */
@Component({
  selector: 'app-sales-view',
  imports: [RouterLink, Alert],
  templateUrl: './sales-view.component.html',
})
export class SalesView implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(SalesService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly dateTime = formatDateTime;
  protected readonly durationText = formatDuration;
  readonly sale = signal<Sale | null>(null);
  readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      this.sale.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Sale not found.'));
    }
  }
}
