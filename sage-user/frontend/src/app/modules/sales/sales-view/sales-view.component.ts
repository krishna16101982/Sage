import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { AuthService } from '../../../services/auth/auth.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { Sale } from '../../../shared/interfaces/sale.interface';
import { formatDate } from '../../../shared/utils/dates';
import { formatDateTime, formatDuration, formatInr } from '../../../shared/utils/format';
import { SalesCancelModal } from './sales-cancel-modal/sales-cancel-modal.component';

/** /sales/:id — one sale (own, or a team member's for a manager), with Cancel. */
@Component({
  selector: 'app-sales-view',
  imports: [RouterLink, Alert, SalesCancelModal],
  templateUrl: './sales-view.component.html',
})
export class SalesView implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(SalesService);
  private readonly auth = inject(AuthService);
  protected readonly inr = formatInr;
  protected readonly date = formatDate;
  protected readonly dateTime = formatDateTime;
  protected readonly durationText = formatDuration;

  readonly sale = signal<Sale | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());
  readonly cancelling = signal(false);
  /** A manager looking at a team member's sale goes back to Team sales. */
  readonly backLink = computed(() => (this.sale() && this.sale()!.sellerId !== this.auth.user()?.id ? '/sales/team' : '/sales/list'));

  async ngOnInit(): Promise<void> {
    try {
      this.sale.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Sale not found.'));
    }
  }

  onCancelled(sale: Sale): void {
    this.sale.set(sale);
    this.cancelling.set(false);
    this.notice.set(`Sale ${sale.saleNumber} cancelled.`);
  }
}
