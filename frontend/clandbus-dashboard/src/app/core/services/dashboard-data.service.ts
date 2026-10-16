import { Injectable } from '@angular/core';
import { BehaviorSubject, catchError, forkJoin, of, Subscription, switchMap } from 'rxjs';
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
  private loadRequestId = 0;
  private loadSubscription?: Subscription;

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
    this.canViewTeam$.next(false);
    this.teamOwners$.next([]); this.selectedOwner$.next('');
    this.status$.next('Inicia sesión para consultar tus métricas');
  }

  loadLocal() {
    const owner = this.selectedOwner$.value;
    const requestId = ++this.loadRequestId;
    this.loadSubscription?.unsubscribe();
    this.loadSubscription = this.readDashboard(owner).subscribe({
      next: (data: any) => {
        if (requestId !== this.loadRequestId) return;
        this.summary$.next({ ...EMPTY, ...data.summary }); this.cases$.next(data.cases || []); this.tasks$.next(data.tasks || []);
        this.status$.next(data.summary?.lastSyncAt ? `Última captura ${new Date(data.summary.lastSyncAt).toLocaleString('es-MX')}` : 'Esperando primera sincronización');
      },
      error: () => {
        if (requestId === this.loadRequestId) this.status$.next('No fue posible leer la base local');
      }
    });
  }

  sessionStarted(profile?: UserProfile, announce = true) {
    if (profile) this.profile$.next(profile);
    this.connected$.next(true); this.loadLocal();
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
    this.timer = setInterval(() => this.loadLocal(), 60 * 60 * 1000);
    if (announce) this.notify('Sesión iniciada. Tus métricas están disponibles.', 'success');
  }

  sync(silent = false) {
    if (!silent) this.loading$.next(true);
    this.error$.next(''); this.status$.next('Sincronizando con Acumatica…');
    const owner = this.selectedOwner$.value;
    this.api.synchronize().pipe(
      switchMap(() => this.canViewTeam$.value
        ? this.api.importLatestTasks().pipe(catchError(() => of(null)))
        : of(null)),
      switchMap(() => this.readDashboard(owner))
    ).subscribe({
      next: (data: any) => {
        this.summary$.next({ ...EMPTY, ...data.summary }); this.cases$.next(data.cases || []); this.tasks$.next(data.tasks || []);
        this.loading$.next(false); this.status$.next(`Actualizado ${new Date().toLocaleString('es-MX')}`);
        if (this.canViewTeam$.value) this.refreshOwners();
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
    const owner = this.selectedOwner$.value;
    return this.api.importTasks(file).pipe(
      switchMap((result) => this.readDashboard(owner, result))
    );
  }

  importLatestTasks() {
    this.loading$.next(true);
    this.error$.next('');
    this.status$.next('Leyendo el Excel más reciente…');
    const owner = this.selectedOwner$.value;
    return this.api.importLatestTasks().pipe(
      switchMap((result) => this.readDashboard(owner, result))
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
    if (this.canViewTeam$.value) this.refreshOwners();
  }

  selectOwner(owner: string) {
    if (!this.canViewTeam$.value && owner) return;
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
    return forkJoin({
      ...(result === undefined ? {} : { result: of(result) }),
      summary: this.api.getSummary(owner).pipe(catchError(() => of(this.summary$.value))),
      cases: this.api.getCases(owner).pipe(catchError(() => of(this.cases$.value))),
      tasks: this.api.getTasks(owner).pipe(catchError(() => of(this.tasks$.value)))
    });
  }

  disconnect() {
    if (this.timer) clearInterval(this.timer);
    this.loadSubscription?.unsubscribe();
    this.api.logout().subscribe();
    this.clearPrivateData();
    this.notify('Sesión cerrada correctamente.', 'info');
  }
}
