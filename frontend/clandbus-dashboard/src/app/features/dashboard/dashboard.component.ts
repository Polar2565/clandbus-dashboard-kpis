import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { LucideAngularModule } from 'lucide-angular';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, LucideAngularModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  readonly data = inject(DashboardDataService);
  readonly today = new Date();
  readonly summary$ = this.data.summary$;
  readonly cases$ = this.data.cases$;
  readonly tasks$ = this.data.tasks$;
  selectedCategory = '';
  selectedInsight: 'overdue'|'load'|'urgent'|'' = '';
  donutSegment(value: number, total: number) {
    const percentage = total ? Math.max(0, Math.min(100, (value / total) * 100)) : 0;
    return `${percentage} ${100 - percentage}`;
  }
  donutOffset(previousValues: number, total: number) {
    return total ? -Math.max(0, Math.min(100, (previousValues / total) * 100)) : 0;
  }
  otherActiveCases(active: number, open: number, pending: number) {
    return Math.max(0, active - open - pending);
  }
  migrations(tasks:any[]){return tasks.filter(x=>`${x.category}`.toLowerCase().includes('migr')).length;}
  support(tasks:any[]){return tasks.filter(x=>`${x.category}`.toLowerCase().includes('soporte')).length;}
  certifications(tasks:any[]){return tasks.filter(x=>`${x.category}`.toLowerCase().includes('cert')).length;}
  categoryTasks(tasks:any[], category:string){
    const key=category.toLowerCase();
    return tasks.filter(x=>`${x.category}`.toLowerCase().includes(key));
  }
  pendingTasks(tasks:any[]){
    return tasks.filter(x=>(x.completionPercent||0)<100 && !`${x.status}`.toLowerCase().includes('cancel'))
      .sort((a,b)=>new Date(a.dueAt||'2999-12-31').getTime()-new Date(b.dueAt||'2999-12-31').getTime());
  }
  overduePending(tasks:any[]){return this.pendingTasks(tasks).filter(x=>x.dueAt&&new Date(x.dueAt)<this.today);}
  topPendingCategory(tasks:any[]){
    const totals=new Map<string,number>();
    for(const task of this.pendingTasks(tasks)){const name=`${task.category||'Sin categoría'}`.trim()||'Sin categoría';totals.set(name,(totals.get(name)||0)+1);}
    const top=[...totals.entries()].sort((a,b)=>b[1]-a[1])[0];
    return top?{name:top[0],count:top[1]}:null;
  }
  nextDue(tasks:any[]){return this.pendingTasks(tasks).find(x=>x.dueAt)||null;}
  insightTasks(tasks:any[]){
    if(this.selectedInsight==='overdue')return this.overduePending(tasks);
    if(this.selectedInsight==='load'){const top=this.topPendingCategory(tasks);return top?this.pendingTasks(tasks).filter(x=>`${x.category||'Sin categoría'}`.trim()===top.name):[];}
    if(this.selectedInsight==='urgent'){const next=this.nextDue(tasks);return next?[next]:[];}
    return [];
  }
  insightTitle(tasks:any[]){return this.selectedInsight==='overdue'?'Tareas vencidas':this.selectedInsight==='load'?`Carga pendiente · ${this.topPendingCategory(tasks)?.name||''}`:'Compromiso más urgente';}
  categoryLabel(){
    return this.selectedCategory==='migr'?'Migraciones':this.selectedCategory==='soporte'?'Soporte':'Certificaciones';
  }
}
