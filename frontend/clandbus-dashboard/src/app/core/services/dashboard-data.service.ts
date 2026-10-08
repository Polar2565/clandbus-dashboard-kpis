import { Injectable } from '@angular/core';
import { BehaviorSubject, forkJoin, of, switchMap } from 'rxjs';
import { AcumaticaService } from './acumatica.service';

export interface DashboardSummary {
  activeCases: number; openCases: number; pendingCustomerCases: number; closedCases: number;
  activeTasks: number; completedTasks: number; overdueTasks: number; averageTaskProgress: number;
  lastSyncAt?: string;
}
export interface UserProfile { username: string; displayName: string; ownerId: string; isAuthenticated: boolean; }
export interface AppNotification { message: string; type: 'success' | 'error' | 'info'; }

const EMPTY: DashboardSummary = { activeCases: 0, openCases: 0, pendingCustomerCases: 0, closedCases: 0, activeTasks: 0, completedTasks: 0, overdueTasks: 0, averageTaskProgress: 0 };

@Injectable({ providedIn: 'root' })
export class DashboardDataService {
  readonly summary$ = new BehaviorSubject<DashboardSummary>(EMPTY);
  readonly cases$ = new BehaviorSubject<any[]>([]);
  readonly tasks$ = new BehaviorSubject<any[]>([]);
  readonly connected$ = new BehaviorSubject(false);
  readonly loading$ = new BehaviorSubject(false);
  readonly status$ = new BehaviorSubject('Datos locales listos');
  readonly error$ = new BehaviorSubject('');
  readonly profile$ = new BehaviorSubject<UserProfile | null>(null);
  readonly notification$ = new BehaviorSubject<AppNotification | null>(null);
  private timer?: ReturnType<typeof setInterval>;
  private notificationTimer?: ReturnType<typeof setTimeout>;

  constructor(private api: AcumaticaService) {
    this.api.getCurrentUser().subscribe({
      next: (profile) => profile?.isAuthenticated ? this.sessionStarted(profile, false) : this.clearPrivateData(),
      error: () => this.clearPrivateData()
    });
  }

  notify(message: string, type: AppNotification['type'] = 'info') {
    if (this.notificationTimer) clearTimeout(this.notificationTimer);
    this.notification$.next({ message, type });
    this.notificationTimer = setTimeout(() => this.notification$.next(null), 4500);
  }

  private clearPrivateData() {
    this.summary$.next(EMPTY); this.cases$.next([]); this.tasks$.next([]);
    this.connected$.next(false); this.profile$.next(null);
    this.status$.next('Inicia sesión para consultar tus métricas');
  }

  loadLocal() {
    forkJoin({ summary: this.api.getSummary(), cases: this.api.getCases(), tasks: this.api.getTasks() }).subscribe({
      next: (data: any) => {
        this.summary$.next({ ...EMPTY, ...data.summary }); this.cases$.next(data.cases || []); this.tasks$.next(data.tasks || []);
        this.status$.next(data.summary?.lastSyncAt ? `Última captura ${new Date(data.summary.lastSyncAt).toLocaleString('es-MX')}` : 'Esperando primera sincronización');
      },
      error: () => this.status$.next('No fue posible leer la base local')
    });
  }

  sessionStarted(profile?: UserProfile, announce = true) {
    if (profile) this.profile$.next(profile);
    this.connected$.next(true); this.loadLocal();
    if (this.timer) clearInterval(this.timer);
    this.timer = setInterval(() => this.loadLocal(), 60 * 60 * 1000);
    if (announce) this.notify('Sesión iniciada. Tus métricas están disponibles.', 'success');
  }

  sync(silent = false) {
    if (!silent) this.loading$.next(true);
    this.error$.next(''); this.status$.next('Sincronizando con Acumatica…');
    this.api.synchronize().pipe(switchMap(() => forkJoin({ summary: this.api.getSummary(), cases: this.api.getCases(), tasks: this.api.getTasks() }))).subscribe({
      next: (data: any) => {
        this.summary$.next({ ...EMPTY, ...data.summary }); this.cases$.next(data.cases || []); this.tasks$.next(data.tasks || []);
        this.loading$.next(false); this.status$.next(`Actualizado ${new Date().toLocaleString('es-MX')}`);
      },
      error: () => {
        this.loading$.next(false); this.status$.next('Sesión activa; sincronización pendiente');
        this.error$.next('La sesión inició correctamente, pero Acumatica rechazó la consulta de datos.');
        this.loadLocal();
        this.notify('No hubo cambios nuevos; se mantienen los últimos datos disponibles.', 'info');
      }
    });
  }

  importTasks(file: File) {
    this.loading$.next(true);
    this.error$.next('');
    this.status$.next('Importando Tasks desde Excel…');
    return this.api.importTasks(file).pipe(
      switchMap((result) => forkJoin({
        result: of(result), summary: this.api.getSummary(),
        cases: this.api.getCases(), tasks: this.api.getTasks()
      }))
    );
  }

  importLatestTasks() {
    this.loading$.next(true);
    this.error$.next('');
    this.status$.next('Leyendo el Excel más reciente…');
    return this.api.importLatestTasks().pipe(
      switchMap((result) => forkJoin({
        result: of(result), summary: this.api.getSummary(),
        cases: this.api.getCases(), tasks: this.api.getTasks()
      }))
    );
  }

  applyTaskImport(data: any) {
    this.summary$.next({ ...EMPTY, ...data.summary });
    this.cases$.next(data.cases || []);
    this.tasks$.next(data.tasks || []);
    this.loading$.next(false);
    const imported = data.result?.imported ?? data.tasks?.length ?? 0;
    this.status$.next(`${imported} Tasks importadas desde Excel`);
    this.notify(`${imported} Tasks actualizadas correctamente.`, 'success');
  }

  disconnect() {
    if (this.timer) clearInterval(this.timer);
    this.api.logout().subscribe();
    this.clearPrivateData();
    this.notify('Sesión cerrada correctamente.', 'info');
  }
}
