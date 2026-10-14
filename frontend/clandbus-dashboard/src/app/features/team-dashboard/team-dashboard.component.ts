import { CommonModule } from '@angular/common';
import { Component, HostListener, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { LucideAngularModule } from 'lucide-angular';
import { AcumaticaService } from '../../core/services/acumatica.service';

type Period = 'week' | 'month' | 'year' | 'history';
type Detail = 'completed' | 'active' | 'overdue' | 'cases' | '';

@Component({
  selector: 'app-team-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './team-dashboard.component.html',
  styleUrl: './team-dashboard.component.scss'
})
export class TeamDashboardComponent implements OnInit {
  private readonly api = inject(AcumaticaService);
  private readonly router = inject(Router);
  period: Period = 'week';
  selectedUserKey = '';
  report: any = null;
  loading = true;
  error = '';
  detail: Detail = '';

  ngOnInit() { this.load(); }

  load() {
    this.loading = true;
    this.error = '';
    this.api.getTeamReport(this.period, this.selectedUserKey).subscribe({
      next: report => { this.report = report; this.loading = false; },
      error: error => {
        this.loading = false;
        if (error.status === 403) {
          this.error = 'Tu cuenta no tiene autorización para consultar el consolidado del equipo.';
          return;
        }
        this.error = 'No fue posible cargar el tablero del equipo.';
      }
    });
  }

  selectPeriod(period: Period) {
    if (period === this.period) return;
    this.period = period;
    this.closeDetail();
    this.load();
  }

  selectMember() {
    this.closeDetail();
    this.load();
  }

  openDetail(detail: Detail) {
    this.detail = detail;
    document.body.style.overflow = 'hidden';
  }

  closeDetail() {
    this.detail = '';
    document.body.style.overflow = '';
  }

  @HostListener('document:keydown.escape') onEscape() { this.closeDetail(); }

  detailTitle() {
    return ({
      completed: 'Tareas completadas en el periodo',
      active: 'Tareas actualmente activas',
      overdue: 'Tareas vencidas',
      cases: 'Casos creados en el periodo'
    } as Record<string, string>)[this.detail] || '';
  }

  detailTasks() {
    const tasks = this.report?.tasks || [];
    const from = this.report?.from ? new Date(this.report.from).getTime() : Number.NEGATIVE_INFINITY;
    const to = this.report?.to ? new Date(this.report.to).getTime() : Number.POSITIVE_INFINITY;
    const inRange = (value: string | null) => value && new Date(value).getTime() >= from && new Date(value).getTime() < to;
    if (this.detail === 'completed') return tasks.filter((task: any) => task.completionPercent >= 100 && inRange(task.completedAt || task.dueAt));
    if (this.detail === 'overdue') return tasks.filter((task: any) => task.completionPercent < 100 && task.dueAt && new Date(task.dueAt) < new Date());
    if (this.detail === 'active') return tasks.filter((task: any) => task.completionPercent < 100 && !`${task.status}`.toLowerCase().includes('cancel'));
    return [];
  }

  detailCases() {
    const cases = this.report?.cases || [];
    const from = this.report?.from ? new Date(this.report.from).getTime() : Number.NEGATIVE_INFINITY;
    const to = this.report?.to ? new Date(this.report.to).getTime() : Number.POSITIVE_INFINITY;
    return cases.filter((item: any) => item.createdAt
      && new Date(item.createdAt).getTime() >= from
      && new Date(item.createdAt).getTime() < to);
  }

  maxTrend() {
    return Math.max(1, ...(this.report?.trend || []).flatMap((point: any) => [point.tasksStarted, point.tasksCompleted, point.casesCreated]));
  }

  barHeight(value: number) { return `${Math.max(value ? 8 : 2, value * 100 / this.maxTrend())}%`; }

  categoryColor(index: number) {
    return ['#1688b2', '#2e9b73', '#c8861a', '#6657c8', '#d35b62', '#4c7891'][index % 6];
  }

  categoryWidth(value: number) {
    const total = (this.report?.categories || []).reduce((sum: number, item: any) => sum + item.count, 0);
    return `${total ? value * 100 / total : 0}%`;
  }

  back() { this.router.navigateByUrl('/resumen'); }
}
