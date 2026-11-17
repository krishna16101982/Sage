import type { OrderWording } from '../interfaces/order-form.interface';

/** The wording sections in the order they appear on the order form, with their labels and field size. */
export const WORDING_SECTIONS: { key: keyof OrderWording; label: string; rows: number; wide: boolean }[] = [
  { key: 'signReturnLine', label: 'Sign-and-return line', rows: 1, wide: false },
  { key: 'signReturnInstruction', label: 'Sign-and-return instruction', rows: 2, wide: false },
  { key: 'orderNotes', label: 'Order notes', rows: 2, wide: true },
  { key: 'freeMonthNote', label: 'Free-month note (added automatically when free months > 0)', rows: 1, wide: false },
  { key: 'extraItemTitle', label: 'Extra item: title (optional, e.g. bundled seats)', rows: 1, wide: false },
  { key: 'extraItemDescription', label: 'Extra item: description', rows: 2, wide: false },
  { key: 'productTitle', label: 'Product: title', rows: 1, wide: false },
  { key: 'productDescription', label: 'Product: description', rows: 3, wide: true },
  { key: 'periodLine', label: 'Product: period line', rows: 1, wide: false },
  { key: 'taxInformation', label: 'Tax information', rows: 3, wide: true },
  { key: 'signatureClauses', label: 'Authorized signature clauses (one per line)', rows: 6, wide: true },
  { key: 'footer', label: 'Footer (one line per row)', rows: 2, wide: false },
];

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/** '2026-11-16' -> '16th-November-2026' (the order form's date style). */
export function longDate(date: string): string {
  if (!date) return '';
  const [y, m, d] = date.split('-').map(Number);
  const suffix = d % 10 === 1 && d !== 11 ? 'st' : d % 10 === 2 && d !== 12 ? 'nd' : d % 10 === 3 && d !== 13 ? 'rd' : 'th';
  return `${d}${suffix}-${MONTHS[m - 1]}-${y}`;
}
