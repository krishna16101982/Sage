import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Alert } from '../../../component/alert/alert';
import { PermissionService } from '../../../services/permission/permission.service';
import { ProductsService } from '../../../services/products/products.service';
import { errorText } from '../../../services/rest/global-rest-service';
import { Perm } from '../../../shared/enumarations/permission-codes';
import type { PlanSeatPriceYear, ProductPlan, SeatPriceRange } from '../../../shared/interfaces/product.interface';
import { formatInr } from '../../../shared/utils/format';
import { findSeatRange, fiscalYearOf, fyLabel, fyPeriod, seatRangeLabel } from '../../../shared/utils/seat-prices';

/** One editable row. "From" is not stored: it is always 1 or the previous row's "To" + 1, so ranges never gap or overlap. */
interface Row {
  maxSeats: number | null;
  price: number | null;
  cost: number | null;
}

/**
 * Plan edit page > Seat prices: the price per seat by seat range, per financial year.
 * On the User Panel's order form every seat is charged the price of the range the seat count falls in,
 * using the financial year of the order date.
 */
@Component({
  selector: 'app-product-plan-seat-prices',
  imports: [FormsModule, Alert],
  templateUrl: './product-plan-seat-prices.component.html',
})
export class ProductPlanSeatPrices implements OnInit {
  readonly plan = input.required<ProductPlan>();
  private readonly service = inject(ProductsService);
  readonly canEdit = inject(PermissionService).has(Perm.ProductEdit);
  protected readonly fyLabel = fyLabel;
  protected readonly fyPeriod = fyPeriod;
  protected readonly inr = formatInr;
  protected readonly rangeLabel = seatRangeLabel;
  protected readonly currentFy = fiscalYearOf();

  readonly years = signal<PlanSeatPriceYear[]>([]);
  readonly fiscalYear = signal(this.currentFy);
  readonly rows = signal<Row[]>([]);
  readonly dirty = signal(false);
  readonly loaded = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly showPaste = signal(false);
  pasteText = '';
  copyFrom: number | null = null;
  copyPercent = 5;
  readonly testSeats = signal<number | null>(8);

  /** Years to choose from: those saved, plus last, this and next financial year. */
  readonly yearOptions = computed(() => {
    const set = new Set([...this.years().map((y) => y.fiscalYear), this.currentFy - 1, this.currentFy, this.currentFy + 1, this.fiscalYear()]);
    return [...set].sort((a, b) => b - a);
  });
  readonly savedYears = computed(() => this.years().map((y) => y.fiscalYear));
  readonly isSaved = computed(() => this.savedYears().includes(this.fiscalYear()));

  /** The rows as ranges (From worked out from the previous To). */
  readonly ranges = computed<(SeatPriceRange & { problem: string | null })[]>(() => {
    const rows = this.rows();
    let from = 1;
    return rows.map((r, i) => {
      const last = i === rows.length - 1;
      const max = r.maxSeats === null || `${r.maxSeats}` === '' ? null : Number(r.maxSeats);
      let problem: string | null = null;
      if (max === null && !last) problem = 'Only the last range can be open-ended: enter the last seat.';
      else if (max !== null && (!Number.isInteger(max) || max < from)) problem = `The last seat must be a whole number, ${from.toLocaleString('en-IN')} or more.`;
      else if (r.price === null || `${r.price}` === '' || Number(r.price) < 0) problem = 'Enter a price of 0 or more.';
      else if (r.cost !== null && `${r.cost}` !== '' && Number(r.cost) < 0) problem = 'The cost cannot be negative.';
      const range = {
        minSeats: from,
        maxSeats: max,
        price: Number(r.price),
        cost: r.cost === null || `${r.cost}` === '' ? null : Number(r.cost),
        problem,
      };
      from = max === null ? from : max + 1;
      return range;
    });
  });
  readonly hasProblems = computed(() => this.ranges().some((r) => r.problem));

  /** "8 seats → 1–9 → ₹x × 8 = ₹y", to check the table the way the order form will use it. */
  readonly test = computed(() => {
    const seats = Number(this.testSeats());
    if (!seats || seats < 1 || this.hasProblems()) return null;
    const range = findSeatRange(this.ranges(), seats);
    return { seats, range, total: range ? range.price * seats : 0 };
  });

  async ngOnInit(): Promise<void> {
    try {
      this.years.set(await this.service.seatPrices(this.plan().id));
      const first = this.years().find((y) => y.fiscalYear === this.currentFy) ?? this.years()[0];
      this.show(first?.fiscalYear ?? this.currentFy);
    } catch (err) {
      this.error.set(errorText(err, 'Unable to load the seat prices.'));
    } finally {
      this.loaded.set(true);
    }
  }

  /** Shows one year's ranges (asks first when there are unsaved changes). */
  chooseYear(value: number | string): void {
    const fy = Number(value);
    if (fy === this.fiscalYear()) return;
    if (this.dirty() && !confirm(`Discard the unsaved changes to ${fyLabel(this.fiscalYear())}?`)) {
      // Put the select back.
      const current = this.fiscalYear();
      this.fiscalYear.set(fy);
      queueMicrotask(() => this.fiscalYear.set(current));
      return;
    }
    this.show(fy);
  }

  private show(fy: number): void {
    this.fiscalYear.set(fy);
    const saved = this.years().find((y) => y.fiscalYear === fy);
    this.rows.set(saved ? saved.ranges.map((r) => ({ maxSeats: r.maxSeats, price: r.price, cost: r.cost })) : []);
    this.copyFrom = this.savedYears().find((y) => y !== fy) ?? null;
    this.dirty.set(false);
    this.error.set(null);
    this.notice.set(null);
    this.showPaste.set(false);
  }

  setRow(index: number, key: keyof Row, value: number | null): void {
    this.rows.update((rows) => rows.map((r, i) => (i === index ? { ...r, [key]: value === null || `${value}` === '' ? null : value } : r)));
    this.dirty.set(true);
  }

  addRow(): void {
    this.rows.update((rows) => [...rows, { maxSeats: null, price: null, cost: null }]);
    this.dirty.set(true);
  }

  removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, i) => i !== index));
    this.dirty.set(true);
  }

  /**
   * Paste rows copied from Excel: "1-9  111578", "1  9  111578  90000", "50,000+  45000".
   * Columns: seat range (or first and last seat), price per seat, optional cost per seat. Commas in numbers are ignored.
   */
  applyPaste(): void {
    this.error.set(null);
    const num = (s: string | undefined) => (s === undefined || s.trim() === '' ? null : Number(s.replace(/[₹,\s]/g, '')));
    const rows: Row[] = [];
    let expected = 1;
    const lines = this.pasteText.split(/\r?\n/).map((l) => l.trim()).filter((l) => l);
    for (const [n, line] of lines.entries()) {
      let cells = line.split(/\t+|\s{2,}/).map((c) => c.trim()).filter((c) => c);
      if (cells.length === 1) cells = line.split(/\s+/);
      let from: number | null;
      let to: number | null;
      let rest: string[];
      const pair = cells[0].replace(/,/g, '').match(/^(\d+)\s*[-–]\s*(\d+)$/);
      const open = cells[0].replace(/,/g, '').match(/^(\d+)\s*\+$/);
      if (pair) [from, to, rest] = [Number(pair[1]), Number(pair[2]), cells.slice(1)];
      else if (open) [from, to, rest] = [Number(open[1]), null, cells.slice(1)];
      else {
        from = num(cells[0]);
        const second = (cells[1] ?? '').replace(/,/g, '');
        to = second === '+' || /above/i.test(second) ? null : num(cells[1]);
        rest = cells.slice(2);
      }
      const price = num(rest[0]);
      const cost = num(rest[1]);
      if (from === null || Number.isNaN(from) || (to !== null && Number.isNaN(to)) || price === null || Number.isNaN(price) || (cost !== null && Number.isNaN(cost))) {
        this.error.set(`Line ${n + 1} ("${line}") is not "seats  price  [cost]".`);
        return;
      }
      if (from !== expected) {
        this.error.set(`Line ${n + 1} starts at ${from.toLocaleString('en-IN')} seats; it should start at ${expected.toLocaleString('en-IN')} (no gaps or overlaps).`);
        return;
      }
      rows.push({ maxSeats: to, price, cost });
      expected = to === null ? expected : to + 1;
    }
    if (rows.length === 0) {
      this.error.set('Paste at least one row.');
      return;
    }
    this.rows.set(rows);
    this.dirty.set(true);
    this.showPaste.set(false);
    this.pasteText = '';
  }

  /** Copies another year's ranges, changing prices and costs by the percent given (rounded to the rupee). */
  copyYear(): void {
    const source = this.years().find((y) => y.fiscalYear === Number(this.copyFrom));
    if (!source) return;
    const factor = 1 + (Number(this.copyPercent) || 0) / 100;
    const change = (v: number | null) => (v === null ? null : Math.round(v * factor));
    this.rows.set(source.ranges.map((r) => ({ maxSeats: r.maxSeats, price: change(r.price), cost: change(r.cost) })));
    this.dirty.set(true);
    this.notice.set(`Copied ${fyLabel(source.fiscalYear)} with ${Number(this.copyPercent) >= 0 ? '+' : ''}${Number(this.copyPercent) || 0}%. Check the prices, then save.`);
  }

  async save(): Promise<void> {
    if (this.hasProblems() || this.rows().length === 0) return;
    await this.put(this.ranges().map(({ minSeats, maxSeats, price, cost }) => ({ minSeats, maxSeats, price, cost })));
  }

  async removeYear(): Promise<void> {
    if (!confirm(`Remove all seat prices of ${fyLabel(this.fiscalYear())}? Orders already saved keep their price.`)) return;
    await this.put([]);
  }

  private async put(ranges: SeatPriceRange[]): Promise<void> {
    const fy = this.fiscalYear();
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    try {
      const saved = await this.service.saveSeatPrices(this.plan().id, fy, ranges);
      this.years.update((years) =>
        [...years.filter((y) => y.fiscalYear !== fy), ...(saved.ranges.length ? [saved] : [])].sort((a, b) => b.fiscalYear - a.fiscalYear),
      );
      this.show(fy);
      this.notice.set(saved.ranges.length ? `Seat prices for ${fyLabel(fy)} saved.` : `Seat prices for ${fyLabel(fy)} removed.`);
    } catch (err) {
      this.error.set(errorText(err));
    } finally {
      this.busy.set(false);
    }
  }
}
