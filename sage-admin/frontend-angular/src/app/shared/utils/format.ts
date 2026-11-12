const inr = new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Formats an amount as Indian rupees, e.g. ₹1,24,999.50. */
export const formatInr = (amount: number) => inr.format(amount);

/** "1 Year", "3 Months", "1 Quarter". */
export const formatDuration = (count: number, unit: string) => `${count} ${unit}${count === 1 ? "" : "s"}`;

/** Formats an ISO date for lists, e.g. "12 Nov 2026, 5:17 pm". */
export function formatDateTime(iso: string | null): string {
  return iso ? new Date(iso).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }) : 'Never';
}
