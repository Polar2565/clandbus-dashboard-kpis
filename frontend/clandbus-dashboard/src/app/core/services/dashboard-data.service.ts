import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, map, Subscription, switchMap } from 'rxjs';
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
  readonly canViewTeam$ = new BehaviorSubject(false);
  readonly teamOwners$ = new BehaviorSubject<string[]>([]);
  readonly selectedOwner$ = new BehaviorSubject('');
  readonly notification$ = new BehaviorSubject<AppNotification | null>(null);
  private timer?: ReturnType<typeof setInterval>;
  private notificationTimer?: ReturnType<typeof setTimeout>;
  private inactivityTimer?: ReturnType<typeof setTimeout>;
  private readonly inactivityMs = 30 * 60 * 1000;
  private loadRequestId = 0;
  private loadSubscription?: Subscription;
  private readonly dashboardCache = new Map<string, { summary: DashboardSummary; cases: any[]; tasks: any[] }>();

  constructor(private api: AcumaticaService) {
    ['pointerdown', 'keydown', 'scroll', 'touchstart'].forEach(event =>
      window.addEventListener(event, () => this.registerActivity(), { passive: true })
    );
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
    if (this.inactivityTimer) clearTimeout(this.inactivityTimer);
    this.summary$.next(EMPTY); this.cases$.next([]); this.tasks$.next([]);
    this.connected$.next(false); this.profile$.next(null);
    this.canViewTeam$.next(false);
    this.teamOwners$.next([]); this.selectedOwner$.next('');
    this.status$.next('Inicia sesión para consultar tus métricas');
  }

  loadLocal(force = false) {
    const owner = this.selectedOwner$.value;
    const cacheKey = this.ownerCacheKey(owner);
    const requestId = ++this.loadRequestId;
    this.loadSubscription?.unsubscribe();
    const cached = force ? undefined : this.dashboardCache.get(cacheKey);
    if (cached) {
      this.applyDashboardView(cached);
      this.loading$.next(false);
    } else {
      this.clearDashboardView();
      this.loading$.next(true);
    }
    this.loadSubscription = this.readDashboard(owner).subscribe({
      next: (data: any) => {
        if (requestId !== this.loadRequestId) return;
        this.dashboardCache.set(cacheKey, data);
        this.applyDashboardView(data);
        this.loading$.next(false);
        this.status$.next(data.summary?.lastSyncAt ? `Última captura ${new Date(data.summary.lastSyncAt).toLocaleString('es-MX')}` : 'Esperando primera sincronización');
      },
      error: (error: HttpErrorResponse) => {
        if (requestId === this.loadRequestId) {
          if (error.status === 401) {
            this.clearPrivateData();
            this.notify('La sesión terminó. Inicia sesión nuevamente.', 'info');
            return;
          }
          this.loading$.next(false);
          this.status$.next('No fue posible leer la base local');
        }
      }
    });
  }

  sessionStarted(profile?: UserProfile, announce = true) {
    if (profile) this.profile$.next(profile);
    this.connected$.next(true); this.loadLocal();
    this.registerActivity();
    this.api.getTeamAccess().subscribe({
      next: access => {
        this.canViewTeam$.next(access.canViewTeam);
        if (access.canViewTeam) {
          this.refreshOwners();
        }
      },
      error: () => this.canViewTeam$.next(false)
    });
    if (this.timer) clearInterval(this.timer);
    this.timer = setInterval(() => this.loadLocal(true), 60 * 60 * 1000);
    if (announce) this.notify('Sesión iniciada. Tus métricas están disponibles.', 'success');
  }

  sync(silent = false) {
    if (!silent) this.loading$.next(true);
    this.error$.next(''); this.status$.next('Sincronizando con Acumatica…');
    const owner = this.selectedOwner$.value;
    this.api.synchronize().pipe(
      switchMap(() => this.readDashboard(owner))
    ).subscribe({
      next: (data: any) => {
        this.dashboardCache.clear();
        this.dashboardCache.set(this.ownerCacheKey(owner), data);
        this.applyDashboardView(data);
        this.loading$.next(false); this.status$.next(`Actualizado ${new Date().toLocaleString('es-MX')}`);
        if (this.canViewTeam$.value) this.refreshOwners();
      },
      error: (error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.clearPrivateData();
          this.notify('La sesión terminó. Inicia sesión nuevamente.', 'info');
          return;
        }
        this.loading$.next(false); this.status$.next('Sesión activa; sincronización pendiente');
        this.error$.next('La sesión inició correctamente, pero Acumatica rechazó la consulta de datos.');
        this.loadLocal();
        this.notify('No hubo cambios nuevos; se mantienen los últimos datos disponibles.', 'info');
      }
    });
  }

  selectOwner(owner: string) {
    if (!this.canViewTeam$.value && owner) return;
    if (owner === this.selectedOwner$.value) return;
    this.selectedOwner$.next(owner);
    this.status$.next(owner ? `Consultando a ${owner}…` : 'Consultando todo el equipo…');
    this.loadLocal();
  }

  private refreshOwners() {
    this.api.getTeamOwners().subscribe({
      next: owners => {
        const currentName = this.profile$.value?.displayName || '';
        const names = owners.filter((owner, index, list) =>
          list.findIndex(candidate => this.samePerson(candidate, owner)) === index
        );
        if (currentName && !names.some(owner => this.samePerson(owner, currentName))) names.push(currentName);
        this.teamOwners$.next(names.sort((a, b) => a.localeCompare(b, 'es')));

        const selected = this.selectedOwner$.value;
        const canonicalOwner = names.find(owner => this.samePerson(owner, selected || currentName)) || names[0] || '';
        if (canonicalOwner !== selected) {
          this.selectedOwner$.next(canonicalOwner);
          this.loadLocal();
        }
      },
      error: () => this.teamOwners$.next([])
    });
  }

  private samePerson(left: string, right: string) {
    const normalize = (value: string) => value
      .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
      .trim().toLocaleLowerCase('es-MX');
    const a = normalize(left);
    const b = normalize(right);
    return !!a && !!b && (a === b || a.startsWith(`${b} `) || b.startsWith(`${a} `));
  }

  private readDashboard(owner: string, result?: any) {
    return this.api.getDashboardView(owner).pipe(
      map(view => result === undefined ? view : { ...view, result })
    );
  }

  private applyDashboardView(data: any) {
    this.summary$.next({ ...EMPTY, ...data.summary });
    this.cases$.next(data.cases || []);
    this.tasks$.next(data.tasks || []);
  }

  private ownerCacheKey(owner: string) {
    return owner.normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim().toLocaleLowerCase('es-MX');
  }

  private clearDashboardView() {
    this.summary$.next({ ...EMPTY });
    this.cases$.next([]);
    this.tasks$.next([]);
  }

  disconnect() {
    if (this.timer) clearInterval(this.timer);
    this.loadSubscription?.unsubscribe();
    this.dashboardCache.clear();
    this.api.logout().subscribe();
    this.clearPrivateData();
    this.notify('Sesión cerrada correctamente.', 'info');
  }

  private registerActivity() {
    if (!this.connected$.value) return;
    if (this.inactivityTimer) clearTimeout(this.inactivityTimer);
    this.inactivityTimer = setTimeout(() => {
      this.api.logout().subscribe({ error: () => undefined });
      this.clearPrivateData();
      this.notify('La sesión se cerró por 30 minutos de inactividad.', 'info');
    }, this.inactivityMs);
  }
}
