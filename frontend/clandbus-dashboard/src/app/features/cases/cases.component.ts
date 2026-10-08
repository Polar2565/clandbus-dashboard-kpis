import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DashboardDataService } from '../../core/services/dashboard-data.service';

@Component({
  selector: 'app-cases',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cases.component.html',
  styleUrl: './cases.component.scss',
})
export class CasesComponent {
  readonly data = inject(DashboardDataService);
  search = '';
  status = 'Todos';
  period: 'day' | 'week' | 'month' | 'year' | 'history' = 'history';
  readonly cases$ = this.data.cases$;
  selected: any = null;
  readonly statusColors: Record<string, string> = {
    N: '#6d7f91',
    O: '#087eaa',
    P: '#bd8127',
    C: '#2b946f',
  };
  selectPeriod(period: 'day' | 'week' | 'month' | 'year' | 'history') {
    this.period = period;
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
  periodCases(items: any[]) {
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
      const date = this.activityDate(x);
      return date && date >= start && date <= now;
    });
  }
  activityDate(item: any) {
    const dates = [item.createdAt, item.lastIncomingAt, item.lastOutgoingAt]
      .filter(Boolean)
      .map((x: string) => new Date(x))
      .filter((x: Date) => !Number.isNaN(x.getTime()));
    return dates.length ? new Date(Math.max(...dates.map((x: Date) => x.getTime()))) : null;
  }
  countStatus(items: any[], key: string) {
    return items.filter((x) => this.statusKey(x.status) === key).length;
  }
  active(items: any[]) {
    return items.filter((x) => this.statusKey(x.status) !== 'C').length;
  }
  noReply(items: any[]) {
    return items.filter((x) => !x.lastOutgoingAt).length;
  }
  statusDistribution(items: any[]) {
    return ['O', 'P', 'N', 'C']
      .map((key) => ({
        key,
        label: this.statusLabel(key),
        count: this.countStatus(items, key),
        color: this.statusColors[key],
      }))
      .filter((x) => x.count > 0)
      .map((x) => ({
        ...x,
        percentage: items.length ? Math.round((x.count / items.length) * 100) : 0,
      }));
  }
  pieGradient(items: any[]) {
    const rows = this.statusDistribution(items);
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
  filtered(items: any[]) {
    const q = this.search.toLowerCase().trim();
    return this.periodCases(items).filter(
      (x) =>
        this.matchesStatus(x.status) &&
        (!q || `${x.caseNumber} ${x.subject} ${x.customer}`.toLowerCase().includes(q)),
    );
  }
  private matchesStatus(value: string) {
    if (this.status === 'Todos') return true;
    return this.statusKey(value) === this.status;
  }
  statusKey(value: string) {
    const status = (value || '').trim().toLowerCase();
    const aliases: Record<string, string[]> = {
      N: ['n', 'new', 'nuevo'],
      O: ['o', 'open', 'abierto'],
      P: ['p', 'pending customer', 'pending', 'esperando cliente', 'cliente pendiente'],
      C: ['c', 'closed', 'cerrado', 'released', 'liberado'],
    };
    return (
      Object.keys(aliases).find((key) => aliases[key].includes(status)) || status.toUpperCase()
    );
  }
  statusLabel(value: string) {
    return (
      ({ N: 'Nuevo', O: 'Abierto / en proceso', P: 'Cliente pendiente', C: 'Cerrado' } as any)[
        this.statusKey(value)
      ] || value
    );
  }
  age(item: any) {
    if (!item.createdAt) return '—';
    return (
      Math.max(0, Math.floor((Date.now() - new Date(item.createdAt).getTime()) / 86400000)) +
      ' días'
    );
  }
}
