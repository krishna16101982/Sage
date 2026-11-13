import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { Pagination } from '../../../component/pagination/pagination';
import { SortHeader } from '../../../component/sort-header/sort-header';
import { AuditLogsService } from '../../../services/audit-logs/audit-logs.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { PagedResult } from '../../../shared/interfaces/api.interface';
import type { AuditLogItem, AuditLogOptions } from '../../../shared/interfaces/audit-setting.interface';
import { formatDateTime } from '../../../shared/utils/format';
import { AuditLogsFilter, type AuditLogsFilterValue } from './audit-logs-filter/audit-logs-filter.component';

/**
 * /audit-logs/list — every recorded change and sign-in, newest first: who (admin user, sales user or system), what,
 * on which record, when and from which IP. Click a row for the values before and after.
 */
@Component({
  selector: 'app-audit-logs-list',
  imports: [RouterLink, Alert, Pagination, SortHeader, AuditLogsFilter],
  templateUrl: './audit-logs-list.component.html',
})
export class AuditLogsList implements OnInit {
  private readonly service = inject(AuditLogsService);
  private readonly router = inject(Router);
  protected readonly dateTime = formatDateTime;

  filter: AuditLogsFilterValue = { search: '', entityType: '', action: '', actorType: '', actorId: '', from: '', to: '' };
  page = 1;
  readonly direction = signal<'asc' | 'desc'>('desc');
  readonly options = signal<AuditLogOptions | null>(null);
  readonly data = signal<PagedResult<AuditLogItem> | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.service.options().then(
      (o) => this.options.set(o),
      () => undefined,
    );
    this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const f = this.filter;
    try {
      this.data.set(
        await this.service.list({
          search: f.search.trim() || undefined,
          entityType: f.entityType || undefined,
          action: f.action || undefined,
          actorType: f.actorType || undefined,
          actorId: f.actorId || undefined,
          from: f.from || undefined,
          to: f.to || undefined,
          sortDirection: this.direction(),
          page: this.page,
          pageSize: 50,
        }),
      );
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the audit log.'));
    } finally {
      this.loading.set(false);
    }
  }

  onFilter(value: AuditLogsFilterValue): void {
    this.filter = value;
    this.page = 1;
    this.load();
  }

  sort(direction: 'asc' | 'desc'): void {
    this.direction.set(direction);
    this.page = 1;
    this.load();
  }

  goTo(page: number): void {
    this.page = page;
    this.load();
  }

  open(row: AuditLogItem): void {
    this.router.navigate(['/audit-logs', row.id]);
  }

  /** "Create", "StatusChange" -> "Create", "Status change". */
  actionText(action: string): string {
    const words = action.replace(/([a-z])([A-Z])/g, '$1 $2');
    return words.charAt(0) + words.slice(1).toLowerCase();
  }
}
