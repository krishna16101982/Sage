import type { DurationUnit } from '../interfaces/catalog.interface';

/** Dates on the sale page are plain 'yyyy-MM-dd' strings (no time zone), as the API sends and expects them. */

const pad = (n: number) => String(n).padStart(2, '0');

/** Today's date on this computer, 'yyyy-MM-dd'. */
export function today(): string {
  const d = new Date();
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** First day of this month, 'yyyy-MM-dd'. */
export function firstOfMonth(): string {
  const d = new Date();
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-01`;
}

/** Adds months the way SQL Server's DATEADD does: 31 Jan + 1 month = 28/29 Feb. */
export function addMonths(date: string, months: number): string {
  const [y, m, d] = date.split('-').map(Number);
  const index = y * 12 + (m - 1) + months;
  const year = Math.floor(index / 12);
  const month = (index % 12) + 1;
  const lastDay = new Date(year, month, 0).getDate();
  return `${year}-${pad(month)}-${pad(Math.min(d, lastDay))}`;
}

/** Renewal date = start + duration (same rule as the server). */
export function renewalDate(start: string, unit: DurationUnit, count: number): string {
  if (!start || !count) return '';
  const months = unit === 'Year' ? 12 * count : unit === 'Quarter' ? 3 * count : count;
  return addMonths(start, months);
}

/** '2026-01-15' → '15 Jan 2026'. */
export function formatDate(date: string | null): string {
  if (!date) return '-';
  const [y, m, d] = date.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' });
}
