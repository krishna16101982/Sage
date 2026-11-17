import type { SeatPriceRange } from '../interfaces/product.interface';

/**
 * Indian financial year by the year it ends in: 1 Apr 2026 to 31 Mar 2027 = 2027 = "FY27".
 * `date` defaults to today in India (the order date decides which year's seat prices apply).
 */
export function fiscalYearOf(date: Date = new Date()): number {
  const [year, month] = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata', year: 'numeric', month: 'numeric' })
    .formatToParts(date)
    .filter((p) => p.type === 'year' || p.type === 'month')
    .map((p) => Number(p.value));
  return month >= 4 ? year + 1 : year;
}

/** "FY27". */
export const fyLabel = (fiscalYear: number) => `FY${String(fiscalYear % 100).padStart(2, '0')}`;

/** "Apr 2026 – Mar 2027". */
export const fyPeriod = (fiscalYear: number) => `Apr ${fiscalYear - 1} – Mar ${fiscalYear}`;

/** "1–9", "50,000+". */
export const seatRangeLabel = (r: Pick<SeatPriceRange, 'minSeats' | 'maxSeats'>) =>
  r.maxSeats === null ? `${r.minSeats.toLocaleString('en-IN')}+` : `${r.minSeats.toLocaleString('en-IN')}–${r.maxSeats.toLocaleString('en-IN')}`;

/** The range a seat count falls in (all seats are charged at its price), or null when none covers it. */
export const findSeatRange = <T extends Pick<SeatPriceRange, 'minSeats' | 'maxSeats'>>(ranges: T[], seats: number): T | null =>
  ranges.find((r) => seats >= r.minSeats && (r.maxSeats === null || seats <= r.maxSeats)) ?? null;
