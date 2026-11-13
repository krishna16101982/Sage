import { Component, computed, input } from '@angular/core';

export interface BarPoint {
  /** Short label under the bar (e.g. the day number). Leave empty to skip it. */
  label: string;
  value: number;
  /** Tooltip, e.g. "12 Nov: ₹4,500.00 (3 sales)". */
  title: string;
}

/**
 * Simple column chart drawn with CSS (no chart library): one bar per point, heights relative to the largest value.
 * The colour comes from the surrounding accent class (accent-teal, accent-blue, ...). Same file in both Angular apps.
 */
@Component({
  selector: 'app-bar-chart',
  template: `
    <div class="bar-chart" role="img" [attr.aria-label]="summary()">
      @for (p of points(); track $index) {
        <div class="bar-chart-col" [title]="p.title">
          <div class="bar-chart-bar" [class.is-zero]="p.value <= 0" [style.height.%]="height(p.value)"></div>
          <span class="bar-chart-label">{{ p.label }}</span>
        </div>
      }
    </div>
  `,
})
export class BarChart {
  readonly points = input.required<BarPoint[]>();
  /** Read out by screen readers instead of the bars. */
  readonly summary = input('Bar chart');
  private readonly max = computed(() => Math.max(0, ...this.points().map((p) => p.value)));

  /** Bar height in % of the plot area (the label row takes the rest). */
  height(value: number): number {
    const max = this.max();
    return max > 0 && value > 0 ? Math.max(3, (value / max) * 85) : 1;
  }
}
