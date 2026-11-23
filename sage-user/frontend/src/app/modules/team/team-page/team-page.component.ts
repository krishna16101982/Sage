import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { initials } from '../../../component/icon/icon';
import { Pagination } from '../../../component/pagination/pagination';
import { MessageService } from '../../../services/message/message.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { SalesService } from '../../../services/sales/sales.service';
import type { CancelRequest, TeamMember } from '../../../shared/interfaces/sale.interface';
import { formatDateTime, formatInr } from '../../../shared/utils/format';
import { CancelDecisionModal } from '../../sales/cancel-decision-modal/cancel-decision-modal.component';

/**
 * /team (sales managers) — the people who report to you:
 *   * "Can request cancellation" per person: when on, they see Request cancellation on their own sales.
 *   * Cancellation requests waiting for you: approve (the sale is cancelled) or reject.
 *   * Add sales person, and Activate / Deactivate a team member (deactivating signs them out at once).
 */
@Component({
  selector: 'app-team-page',
  imports: [RouterLink, Alert, Pagination, CancelDecisionModal],
  templateUrl: './team-page.component.html',
})
export class TeamPage implements OnInit {
  private readonly service = inject(SalesService);
  protected readonly inr = formatInr;
  protected readonly dateTime = formatDateTime;

  readonly members = signal<TeamMember[] | null>(null);
  readonly requests = signal<CancelRequest[] | null>(null);
  /** Waiting requests: one page at a time from the database. */
  readonly requestPage = signal<{ page: number; totalPages: number; totalCount: number } | null>(null);
  requestsPageNo = 1;
  readonly saving = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(inject(MessageService).take());
  readonly deciding = signal<{ request: CancelRequest; approve: boolean } | null>(null);

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    try {
      const [members, requests] = await Promise.all([this.service.teamMembers(), this.service.cancelRequests('Pending', this.requestsPageNo, 20)]);
      this.members.set(members);
      this.requests.set(requests.items);
      this.requestPage.set({ page: requests.page, totalPages: requests.totalPages, totalCount: requests.totalCount });
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load your team.'));
    }
  }

  async requestsGoTo(page: number): Promise<void> {
    this.requestsPageNo = page;
    await this.load();
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

  /** Activates or deactivates a team member; deactivating asks first and signs them out. */
  async setStatus(member: TeamMember, status: 'Active' | 'Inactive'): Promise<void> {
    if (status === 'Inactive' && !confirm(`Deactivate ${member.name}? They are signed out and cannot sign in until you activate them again.`)) return;
    this.error.set(null);
    this.notice.set(null);
    this.saving.set(member.id);
    try {
      const updated = await this.service.setTeamMemberStatus(member.id, status);
      this.members.update((list) => (list ?? []).map((m) => (m.id === member.id ? updated : m)));
      this.notice.set(status === 'Active' ? `${member.name} is active again.` : `${member.name} is deactivated and signed out.`);
    } catch (err) {
      this.error.set(errorText(err));
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
