import {
  activeTasks,
  calculateTaskCompliance,
  caseStatusKey,
  completedTasksInRange,
  createdCasesInRange,
  latestCaseActivity,
  periodRange,
  startedTasksInRange,
  taskIsOverdue
} from './kpi.utils';

describe('kpi.utils', () => {
  const reference = new Date('2026-10-08T12:00:00-06:00');

  it('calcula la semana desde el lunes y no incluye fechas futuras', () => {
    const range = periodRange('week', reference);
    expect(range.start.getDay()).toBe(1);
    expect(range.start.getHours()).toBe(0);
    expect(range.end).toEqual(reference);
  });

  it('separa tareas iniciadas de tareas concluidas en el periodo', () => {
    const tasks = [
      { startAt: '2026-09-01T10:00:00-06:00', completedAt: '2026-10-07T10:00:00-06:00', completionPercent: 100 },
      { startAt: '2026-10-06T10:00:00-06:00', completionPercent: 20 }
    ];
    const range = periodRange('week', reference);
    expect(startedTasksInRange(tasks, range).length).toBe(1);
    expect(completedTasksInRange(tasks, range).length).toBe(1);
  });

  it('no cuenta canceladas o completadas como carga activa ni vencida', () => {
    const tasks = [
      { status: 'Cancelado', dueAt: '2026-10-01', completionPercent: 0 },
      { status: 'Completado', dueAt: '2026-10-01', completionPercent: 100 },
      { status: 'Procesando', dueAt: '2026-10-01', completionPercent: 60 }
    ];
    expect(activeTasks(tasks).length).toBe(1);
    expect(tasks.filter(task => taskIsOverdue(task, reference)).length).toBe(1);
  });

  it('marca cumplimiento temporal como no disponible cuando faltan fechas', () => {
    const result = calculateTaskCompliance([{ status: 'Completado', completionPercent: 100 }], reference);
    expect(result.completed).toBe(1);
    expect(result.completedOnTime).toBeNull();
    expect(result.onTimeRate).toBeNull();
    expect(result.averageCycleDays).toBeNull();
  });

  it('calcula cumplimiento a tiempo y ciclo solo con evidencia completa', () => {
    const result = calculateTaskCompliance([
      { status: 'Completado', startAt: '2026-10-01', completedAt: '2026-10-03', dueAt: '2026-10-04' },
      { status: 'Completado', startAt: '2026-10-01', completedAt: '2026-10-06', dueAt: '2026-10-04' }
    ], reference);
    expect(result.onTimeRate).toBe(50);
    expect(result.averageCycleDays).toBe(4);
  });

  it('cuenta casos creados por createdAt, no por actividad de correo', () => {
    const range = periodRange('week', reference);
    const cases = [
      { createdAt: '2026-09-01', lastIncomingAt: '2026-10-07' },
      { createdAt: '2026-10-06' }
    ];
    expect(createdCasesInRange(cases, range).length).toBe(1);
    expect(latestCaseActivity(cases[0])?.toISOString()).toBe('2026-10-07T00:00:00.000Z');
  });

  it('normaliza estados conocidos sin convertir desconocidos en cerrados', () => {
    expect(caseStatusKey('Esperando cliente')).toBe('pendingCustomer');
    expect(caseStatusKey('Released')).toBe('closed');
    expect(caseStatusKey('Personalizado')).toBe('unknown');
  });
});
