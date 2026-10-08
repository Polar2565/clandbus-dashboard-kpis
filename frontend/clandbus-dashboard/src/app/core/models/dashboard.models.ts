export type AnalysisPeriod = 'day' | 'week' | 'month' | 'year' | 'history' | 'custom';

export interface DashboardTask {
  externalId?: string;
  summary?: string;
  status?: string;
  completionPercent?: number | null;
  category?: string;
  owner?: string;
  relatedCase?: string;
  startAt?: string | null;
  dueAt?: string | null;
  completedAt?: string | null;
  capturedAt?: string | null;
}

export interface DashboardCase {
  caseNumber?: string;
  subject?: string;
  customer?: string;
  category?: string;
  status?: string;
  reason?: string;
  owner?: string;
  createdAt?: string | null;
  lastIncomingAt?: string | null;
  lastOutgoingAt?: string | null;
  capturedAt?: string | null;
}

export interface DateRange {
  start: Date;
  end: Date;
}

export interface TaskComplianceKpis {
  total: number;
  completed: number;
  pending: number;
  overdue: number;
  completionRate: number | null;
  completedOnTime: number | null;
  onTimeRate: number | null;
  averageCycleDays: number | null;
}

