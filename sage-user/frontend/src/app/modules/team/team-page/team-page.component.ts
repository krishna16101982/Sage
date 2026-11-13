import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { initials } from '../../../component/icon/icon';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { CancelRequest, TeamMember } from '../../../shared/interfaces/sale.interface';
import { formatDateTime, formatInr } from '../../../shared/utils/format';
import { CancelDecisionModal } from '../../sales/cancel-decision-modal/cancel-decision-modal.component';

/**
 * /team (sales managers) — the people who report to you:
 *   * "Can request cancellation" per person: when on, they see Request cancellation on their own sales.
 *   * Cancellation requests waiting for you: approve (the sale is cancelled) or reject.
 */
@Component({
  selector: 'app-team-page',
  imports: [RouterLink, Alert, CancelDecisionModal],
  templateUrl: './team-page.component.html',
})
export class TeamPage implements OnInit {
  private readonly service = inject(SalesService);
  protected readonly inr = formatInr;
  protected readonly dateTime = formatDateTime;

  readonly members = signal<TeamMember[] | null>(null);
  readonly requests = signal<CancelRequest[] | null>(null);
  readonly saving = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly deciding = signal<{ request: CancelRequest; approve: boolean } | null>(null);

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    try {
      const [members, requests] = await Promise.all([this.service.teamMembers(), this.service.cancelRequests('Pending')]);
      this.members.set(members);
      this.requests.set(requests);
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load your team.'));
    }
  }

  initialsOf(name: string): string {
    const [first, last] = name.trim().split(/\s+/);
    return initials(first, last);
  }

  async toggle(member: TeamMember, allowed: boolean): Promise<void> {
    this.error.set(null);
    this.notice.set(null);
    this.saving.set(member.id);
    try {
      const updated = await this.service.setCancelPermission(member.id, allowed);
      this.members.update((list) => (list ?? []).map((m) => (m.id === member.id ? updated : m)));
      this.notice.set(allowed ? `${member.name} can now request cancellations.` : `${member.name} can no longer request cancellations.`);
    } catch (err) {
      this.error.set(errorText(err));
      await this.load(); // put the switch back
    } finally {
      this.saving.set(null);
    }
  }

  async decided(message: string): Promise<void> {
    this.deciding.set(null);
    this.notice.set(message);
    await this.load();
  }
}
