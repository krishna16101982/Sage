import { Component, OnDestroy, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { AuditActorType, AuditLogOptions } from '../../../../shared/interfaces/audit-setting.interface';

export interface AuditLogsFilterValue {
  search: string;
  entityType: string;
  action: string;
  actorType: AuditActorType | '';
  actorId: string;
  from: string;
  to: string;
}

/** Filters above the audit log. Typing in the search box waits a moment before emitting. */
@Component({
  selector: 'app-audit-logs-filter',
  imports: [FormsModule],
  templateUrl: './audit-logs-filter.component.html',
})
export class AuditLogsFilter implements OnDestroy {
  readonly options = input<AuditLogOptions | null>(null);
  readonly filterChange = output<AuditLogsFilterValue>();
  search = '';
  entityType = '';
  action = '';
  actorType: AuditActorType | '' = '';
  actorId = '';
  from = '';
  to = '';
  private timer: ReturnType<typeof setTimeout> | undefined;

  /** Picking one admin implies "admin users"; picking another kind of actor clears the admin. */
  pickActorType(): void {
    if (this.actorType !== 'admin') this.actorId = '';
    this.changed(false);
  }

  pickAdmin(): void {
    if (this.actorId) this.actorType = 'admin';
    this.changed(false);
  }

  changed(debounce: boolean): void {
    clearTimeout(this.timer);
    const emit = () =>
      this.filterChange.emit({
        search: this.search, entityType: this.entityType, action: this.action, actorType: this.actorType, actorId: this.actorId, from: this.from, to: this.to,
      });
    if (debounce) this.timer = setTimeout(emit, 250);
    else emit();
  }

  clear(): void {
    this.search = this.entityType = this.action = this.actorId = this.from = this.to = '';
    this.actorType = '';
    this.changed(false);
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
