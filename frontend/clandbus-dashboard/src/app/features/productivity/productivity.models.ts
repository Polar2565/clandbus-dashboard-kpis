export type ActivityType = 'Migration' | 'Case' | 'Certification' | 'Development' | 'Other';
export interface ProfessionalRecord { id:number; activityType:ActivityType; relatedActivityId?:string; relatedActivityName?:string; result?:string; challenge?:string; solution?:string; learning?:string; improvementArea?:string; date:string; notes?:string; createdAt?:string; updatedAt?:string; }
export type ProfessionalRecordInput = Omit<ProfessionalRecord,'id'|'createdAt'|'updatedAt'>;
export interface TaskItem { externalId?:string; summary?:string; category?:string; status?:string; completionPercent?:number; startAt?:string; dueAt?:string; completedAt?:string; }
export interface CaseItem { caseNumber?:string; subject?:string; status?:string; createdAt?:string; lastIncomingAt?:string; lastOutgoingAt?:string; }
export interface PeriodRange { start?:Date; end?:Date; label:string; }
