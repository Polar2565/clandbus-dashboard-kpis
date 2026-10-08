import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { categoryVisual } from '../../core/utils/category.utils';
@Component({
  selector: 'app-tasks',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tasks.component.html',
  styleUrls: ['./tasks.component.scss', './tasks.detail.scss'],
})
export class TasksComponent {
  readonly data = inject(DashboardDataService);
  readonly tasks$ = this.data.tasks$;
  importing = false;
  showOverdueOnly = false;
  overdueModalTasks: any[] = [];
  selectedTask: any = null;
  message = '';
  isError = false;
  period: 'day' | 'week' | 'month' | 'year' | 'history' = 'week';
  readonly categoryVisual = categoryVisual;

  overdue(t: any) {
    return (
      t.dueAt &&
      new Date(t.dueAt) < new Date() &&
      Number(t.completionPercent) < 100 &&
      !`${t.status || ''}`.toLowerCase().includes('cancel')
    );
  }
  completed(items: any[]) {
    return items.filter(
      (x) => Number(x.completionPercent) >= 100 || `${x.status}`.toLowerCase().includes('complet'),
    ).length;
  }
  overdueCount(items: any[]) {
    return items.filter((x) => this.overdue(x)).length;
  }
  openOverdue(items: any[]) {
    this.overdueModalTasks = items.filter((x) => this.overdue(x));
  }
  migrations(items: any[]) {
    return items.filter((x) => `${x.category}`.toLowerCase().includes('migr')).length;
  }
  average(items: any[]) {
    return items.length
      ? Math.round(
          items.reduce((sum, x) => sum + Number(x.completionPercent || 0), 0) / items.length,
        )
      : 0;
  }
  completionRate(items: any[]) {
    return items.length ? Math.round((this.completed(items) / items.length) * 100) : 0;
  }
  selectPeriod(period: 'day' | 'week' | 'month' | 'year' | 'history') {
    this.period = period;
    this.showOverdueOnly = false;
  }
  displayedTasks(items: any[]) {
    return this.showOverdueOnly ? items.filter((x) => this.overdue(x)) : items;
  }
  filtered(items: any[]) {
    if (this.period === 'history') return items;
    const now = new Date();
    let start = new Date(now);
    if (this.period === 'day') start.setHours(0, 0, 0, 0);
    if (this.period === 'week') {
      const day = (start.getDay() + 6) % 7;
      start.setDate(start.getDate() - day);
      start.setHours(0, 0, 0, 0);
    }
    if (this.period === 'month') start = new Date(now.getFullYear(), now.getMonth(), 1);
    if (this.period === 'year') start = new Date(now.getFullYear(), 0, 1);
    return items.filter((x) => {
      const finished =
        Number(x.completionPercent) >= 100 || `${x.status}`.toLowerCase().includes('complet');
      const date = finished ? x.completedAt : x.startAt;
      if (finished && !date) return false;
      return date && new Date(date) >= start && new Date(date) <= now;
    });
  }
  previous(items: any[]) {
    if (this.period === 'history') return [];
    const now = new Date();
    let start = new Date(now),
      previousStart = new Date(now),
      previousEnd = new Date(now);
    if (this.period === 'day') {
      start.setHours(0, 0, 0, 0);
      previousEnd = new Date(start);
      previousStart = new Date(start);
      previousStart.setDate(previousStart.getDate() - 1);
    }
    if (this.period === 'week') {
      const day = (start.getDay() + 6) % 7;
      start.setDate(start.getDate() - day);
      start.setHours(0, 0, 0, 0);
      previousEnd = new Date(start);
      previousStart = new Date(start);
      previousStart.setDate(previousStart.getDate() - 7);
    }
    if (this.period === 'month') {
      start = new Date(now.getFullYear(), now.getMonth(), 1);
      previousEnd = new Date(start);
      previousStart = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    }
    if (this.period === 'year') {
      start = new Date(now.getFullYear(), 0, 1);
      previousEnd = new Date(start);
      previousStart = new Date(now.getFullYear() - 1, 0, 1);
    }
    return items.filter((x) => {
      const finished =
        Number(x.completionPercent) >= 100 || `${x.status}`.toLowerCase().includes('complet');
      const date = finished ? x.completedAt : x.startAt;
      if (!date) return false;
      const value = new Date(date);
      return value >= previousStart && value < previousEnd;
    });
  }
  delta(items: any[]) {
    return this.completed(this.filtered(items)) - this.completed(this.previous(items));
  }
  averageCycleDays(items: any[]) {
    const rows = items.filter((x) => x.startAt && x.completedAt);
    return rows.length
      ? Math.round(
          rows.reduce(
            (sum, x) =>
              sum +
              Math.max(
                0,
                (new Date(x.completedAt).getTime() - new Date(x.startAt).getTime()) / 86400000,
              ),
            0,
          ) / rows.length,
        )
      : null;
  }
  periodLabel() {
    return (
      {
        day: 'hoy',
        week: 'esta semana',
        month: 'este mes',
        year: 'este año',
        history: 'todo el histórico',
      } as any
    )[this.period];
  }
  categoryDistribution(items: any[]) {
    const totals = new Map<string, number>();
    for (const task of items) {
      const name = `${task.category || 'Sin categoría'}`.trim() || 'Sin categoría';
      totals.set(name, (totals.get(name) || 0) + 1);
    }
    return [...totals.entries()]
      .sort((a, b) => b[1] - a[1])
      .map(([name, count]) => ({
        name,
        count,
        percentage: items.length ? Math.round((count / items.length) * 100) : 0,
        color: categoryVisual(name).color,
        soft: categoryVisual(name).soft,
      }));
  }
  pieGradient(items: any[]) {
    const rows = this.categoryDistribution(items);
    let cursor = 0;
    if (!rows.length) return '#e7edf1';
    return `conic-gradient(${rows
      .map((row) => {
        const start = cursor;
        cursor += (row.count / items.length) * 100;
        return `${row.color} ${start}% ${cursor}%`;
      })
      .join(',')})`;
  }
  topCategory(items: any[]) {
    return this.categoryDistribution(items)[0];
  }

  selectExcel(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.importing = true;
    this.message = '';
    this.isError = false;
    this.data.importTasks(file).subscribe({
      next: (result) => {
        this.data.applyTaskImport(result);
        this.importing = false;
        this.message = `${result.result?.imported ?? 0} Tasks de tu usuario fueron importadas.`;
        input.value = '';
      },
      error: (error) => {
        this.importing = false;
        this.isError = true;
        this.message = error?.error?.message || 'No fue posible importar el Excel.';
        input.value = '';
      },
    });
  }

  importLatest() {
    this.importing = true;
    this.message = '';
    this.isError = false;
    this.data.importLatestTasks().subscribe({
      next: (result) => {
        this.data.applyTaskImport(result);
        this.importing = false;
      },
      error: (error) => {
        this.importing = false;
        this.isError = true;
        this.data.notify(
          error?.error?.message || 'No fue posible leer la carpeta de importaciones.',
          'error',
        );
      },
    });
  }
}
