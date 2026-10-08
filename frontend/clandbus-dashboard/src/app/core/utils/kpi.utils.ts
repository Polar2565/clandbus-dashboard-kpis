import {
  AnalysisPeriod,
  DashboardCase,
  DashboardTask,
  DateRange,
  TaskComplianceKpis
} from '../models/dashboard.models';

const DAY_MS = 86_400_000;

export function validDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date;
}

export function periodRange(
  period: Exclude<AnalysisPeriod, 'history'>,
  reference = new Date(),
  custom?: DateRange
): DateRange {
  if (period === 'custom') {
    if (!custom || custom.start.getTime() > custom.end.getTime()) {
      throw new Error('El rango personalizado no es válido.');
    }
    return custom;
  }

  const end = new Date(reference);
  const start = new Date(reference);
  if (period === 'day') start.setHours(0, 0, 0, 0);
  if (period === 'week') {
    const daysSinceMonday = (start.getDay() + 6) % 7;
    start.setDate(start.getDate() - daysSinceMonday);
    start.setHours(0, 0, 0, 0);
  }
  if (period === 'month') {
    start.setFullYear(reference.getFullYear(), reference.getMonth(), 1);
    start.setHours(0, 0, 0, 0);
  }
  if (period === 'year') {
    start.setFullYear(reference.getFullYear(), 0, 1);
    start.setHours(0, 0, 0, 0);
  }
  return { start, end };
}

export function isInRange(value: string | null | undefined, range: DateRange): boolean {
  const date = validDate(value);
  return !!date && date >= range.start && date <= range.end;
}

export function taskIsCompleted(task: DashboardTask): boolean {
  const status = normalize(task.status);
  return Number(task.completionPercent ?? 0) >= 100 ||
    ['completado', 'completada', 'completed', 'terminado', 'terminada', 'closed', 'cerrado', 'cerrada']
      .some(value => status.includes(value));
}

export function taskIsCancelled(task: DashboardTask): boolean {
  const status = normalize(task.status);
  return ['cancelado', 'cancelada', 'cancelled', 'canceled'].some(value => status.includes(value));
}

export function taskIsOverdue(task: DashboardTask, reference = new Date()): boolean {
  const dueAt = validDate(task.dueAt);
  return !taskIsCompleted(task) && !taskIsCancelled(task) && !!dueAt && dueAt < reference;
}

/** Tareas concluidas dentro del periodo. No sustituye a la carga activa del momento. */
export function completedTasksInRange(tasks: DashboardTask[], range: DateRange): DashboardTask[] {
  return tasks.filter(task => taskIsCompleted(task) && isInRange(task.completedAt, range));
}

/** Tareas iniciadas dentro del periodo; evita atribuir una tarea al periodo por su fecha de cierre. */
export function startedTasksInRange(tasks: DashboardTask[], range: DateRange): DashboardTask[] {
  return tasks.filter(task => isInRange(task.startAt, range));
}

/** Casos creados en el periodo. La actividad de correo no se interpreta como creación. */
export function createdCasesInRange(cases: DashboardCase[], range: DateRange): DashboardCase[] {
  return cases.filter(item => isInRange(item.createdAt, range));
}

export function latestCaseActivity(item: DashboardCase): Date | null {
  const dates = [item.createdAt, item.lastIncomingAt, item.lastOutgoingAt]
    .map(validDate)
    .filter((date): date is Date => date !== null);
  return dates.length ? new Date(Math.max(...dates.map(date => date.getTime()))) : null;
}

export function activeTasks(tasks: DashboardTask[]): DashboardTask[] {
  return tasks.filter(task => !taskIsCompleted(task) && !taskIsCancelled(task));
}

export function calculateTaskCompliance(
  tasks: DashboardTask[],
  reference = new Date()
): TaskComplianceKpis {
  const completed = tasks.filter(taskIsCompleted);
  const pending = activeTasks(tasks);
  const withDeadlineEvidence = completed.filter(task => validDate(task.completedAt) && validDate(task.dueAt));
  const completedOnTime = withDeadlineEvidence.filter(task =>
    validDate(task.completedAt)!.getTime() <= validDate(task.dueAt)!.getTime()
  ).length;
  const withCycleEvidence = completed.filter(task => validDate(task.startAt) && validDate(task.completedAt));
  const totalCycleDays = withCycleEvidence.reduce((total, task) => {
    const start = validDate(task.startAt)!;
    const end = validDate(task.completedAt)!;
    return total + Math.max(0, (end.getTime() - start.getTime()) / DAY_MS);
  }, 0);

  return {
    total: tasks.length,
    completed: completed.length,
    pending: pending.length,
    overdue: pending.filter(task => taskIsOverdue(task, reference)).length,
    completionRate: tasks.length ? Math.round(completed.length / tasks.length * 100) : null,
    completedOnTime: withDeadlineEvidence.length ? completedOnTime : null,
    onTimeRate: withDeadlineEvidence.length
      ? Math.round(completedOnTime / withDeadlineEvidence.length * 100)
      : null,
    averageCycleDays: withCycleEvidence.length
      ? Math.round(totalCycleDays / withCycleEvidence.length)
      : null
  };
}

export type CaseStatusKey = 'new' | 'open' | 'pendingCustomer' | 'closed' | 'unknown';

export function caseStatusKey(value: string | null | undefined): CaseStatusKey {
  const status = normalize(value);
  if (['n', 'new', 'nuevo'].includes(status)) return 'new';
  if (['o', 'open', 'abierto', 'en proceso', 'in process'].includes(status)) return 'open';
  if (['p', 'pending customer', 'pending', 'esperando cliente', 'cliente pendiente'].includes(status)) {
    return 'pendingCustomer';
  }
  if (['c', 'closed', 'cerrado', 'released', 'liberado'].includes(status)) return 'closed';
  return 'unknown';
}

function normalize(value: unknown): string {
  return `${value ?? ''}`.trim().toLocaleLowerCase('es-MX');
}

