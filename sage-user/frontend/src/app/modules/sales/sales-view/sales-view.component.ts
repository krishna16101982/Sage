import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { AuthService } from '../../../services/auth/auth.service';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { CancelRequest, Sale } from '../../../shared/interfaces/sale.interface';
import { formatDate } from '../../../shared/utils/dates';
import { formatDateTime, formatDuration, formatInr } from '../../../shared/utils/format';
import { CancelDecisionModal } from '../cancel-decision-modal/cancel-decision-modal.component';
import { SalesCancelModal } from './sales-cancel-modal/sales-cancel-modal.component';

/**
 * /sales/:id — one sale (own, or a team member's for a manager).
 * Cancelling: a sales manager cancels directly and approves or rejects requests from the team. A sales person sees
 * "Request cancellation" only when their manager allowed it; the sale is cancelled only when the manager approves.
 */
@Component({
  selector: 'app-sales-view',
  imports: [RouterLink, Alert, SalesCancelModal, CancelDecisionModal],
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
  /** Which dialog is open. */
  readonly dialog = signal<'cancel' | 'request' | null>(null);
  readonly deciding = signal<{ request: CancelRequest; approve: boolean } | null>(null);

  readonly isMine = computed(() => this.sale()?.sellerId === this.auth.user()?.id);
  /** A manager looking at a team member's sale goes back to Team sales. */
  readonly backLink = computed(() => (this.sale() && !this.isMine() ? '/sales/team' : '/sales/list'));
  readonly pendingRequest = computed(() => this.sale()?.cancelRequests?.find((r) => r.status === 'Pending') ?? null);
  readonly latestRequest = computed(() => this.sale()?.cancelRequests?.[0] ?? null);

  /** Sales managers cancel directly (their own sales and their team's). */
  readonly canCancel = computed(() => this.sale()?.status === 'Active' && !!this.auth.user()?.isManager);
  /** A sales person their manager allowed, for their own active sale with no request waiting. */
  readonly canRequest = computed(
    () => this.sale()?.status === 'Active' && this.isMine() && !!this.auth.user()?.canRequestCancellation && !this.pendingRequest(),
  );
  /** The manager of the seller decides a waiting request. */
  readonly canDecide = computed(() => !!this.pendingRequest() && !!this.auth.user()?.isManager && !this.isMine());

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  private async load(): Promise<void> {
    try {
      this.sale.set(await this.service.get(this.id()));
    } catch (err) {
      this.error.set(errorText(err, 'Sale not found.'));
    }
  }

  async finished(message: string): Promise<void> {
    this.dialog.set(null);
    this.deciding.set(null);
    this.notice.set(message);
    await this.load();
  }
}
