/** Mirrors the Admin API's AuditLogItem / AuditLogDetail / AuditLogOptions and SettingDto. */

export type AuditActorType = 'admin' | 'sales' | 'system';

export interface AuditLogItem {
  id: number;
  createdAtUtc: string;
  /** admin = Admin Panel user, sales = User Panel user, system = nobody signed in (e.g. a failed sign-in). */
  actorType: AuditActorType;
  actorName: string | null;
  actorUsername: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  ipAddress: string | null;
  hasOldValues: boolean;
  hasNewValues: boolean;
}

/** One entry with the values before and after the change, as recorded (JSON). */
export interface AuditLogDetail extends AuditLogItem {
  oldValues: unknown;
  newValues: unknown;
}

export interface AuditLogOptions {
  entityTypes: string[];
  actions: string[];
  admins: { id: string; name: string }[];
}

export interface AuditLogQuery {
  search?: string;
  entityType?: string;
  action?: string;
  actorType?: AuditActorType;
  actorId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
  sortDirection?: 'asc' | 'desc';
}

export interface Setting {
  key: string;
  value: string | null;
  valueType: 'string' | 'int' | 'bool' | 'json';
  category: string;
  label: string;
  description: string | null;
  minValue: number | null;
  maxValue: number | null;
  maxLength: number | null;
  pattern: string | null;
  updatedAtUtc: string | null;
  updatedByName: string | null;
}
