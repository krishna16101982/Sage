import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Alert } from '../../../component/alert/alert';
import { AuditLogsService } from '../../../services/audit-logs/audit-logs.service';
import { errorText } from '../../../services/rest/global-rest-service';
import type { AuditLogDetail } from '../../../shared/interfaces/audit-setting.interface';
import { formatDateTime } from '../../../shared/utils/format';

interface FieldRow {
  field: string;
  before: string;
  after: string;
  changed: boolean;
}

/** Flattens a recorded JSON value into "field -> text" pairs (nested objects as "Parent.Child"). */
function flatten(value: unknown, prefix = '', out: Record<string, string> = {}): Record<string, string> {
  if (value === null || value === undefined) return out;
  if (typeof value !== 'object' || Array.isArray(value)) {
    out[prefix || 'Value'] = Array.isArray(value) ? value.map((v) => (typeof v === 'object' ? JSON.stringify(v) : String(v))).join(', ') : String(value);
    return out;
  }
  for (const [key, inner] of Object.entries(value as Record<string, unknown>)) {
    const name = prefix ? `${prefix}.${key}` : key;
    if (inner !== null && typeof inner === 'object' && !Array.isArray(inner)) flatten(inner, name, out);
    else out[name] = inner === null || inner === undefined ? '' : Array.isArray(inner) ? inner.join(', ') : String(inner);
  }
  return out;
}

/** /audit-logs/:id — one audit entry: who, what, when, and each field before and after the change. */
@Component({
  selector: 'app-audit-logs-view',
  imports: [RouterLink, Alert],
  templateUrl: './audit-logs-view.component.html',
})
export class AuditLogsView implements OnInit {
  /** From the route (:id). */
  readonly id = input.required<string>();
  private readonly service = inject(AuditLogsService);
  protected readonly dateTime = formatDateTime;
  readonly entry = signal<AuditLogDetail | null>(null);
  readonly error = signal<string | null>(null);
  readonly showRaw = signal(false);

  /** One row per field found before or after; changed fields first. */
  readonly rows = computed<FieldRow[]>(() => {
    const e = this.entry();
    if (!e) return [];
    const before = flatten(e.oldValues);
    const after = flatten(e.newValues);
    const fields = [...new Set([...Object.keys(before), ...Object.keys(after)])];
    const rows = fields.map((field) => ({
      field,
      before: before[field] ?? '',
      after: after[field] ?? '',
      changed: e.hasOldValues && e.hasNewValues && (before[field] ?? '') !== (after[field] ?? ''),
    }));
    return rows.sort((a, b) => Number(b.changed) - Number(a.changed));
  });

  readonly raw = computed(() => {
    const e = this.entry();
    return e ? JSON.stringify({ before: e.oldValues ?? null, after: e.newValues ?? null }, null, 2) : '';
  });

  async ngOnInit(): Promise<void> {
    try {
      this.entry.set(await this.service.get(Number(this.id())));
    } catch (err) {
      this.error.set(errorText(err, 'Audit entry not found.'));
    }
  }

  actionText(action: string): string {
    const words = action.replace(/([a-z])([A-Z])/g, '$1 $2');
    return words.charAt(0) + words.slice(1).toLowerCase();
  }
}
