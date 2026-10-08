import { Routes } from '@angular/router';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { CasesComponent } from './features/cases/cases.component';
import { TasksComponent } from './features/tasks/tasks.component';
import { ProductivityComponent } from './features/productivity/productivity.component';
import { IntegrationComponent } from './features/integration/integration.component';

export const routes: Routes = [
  { path: 'resumen', component: DashboardComponent, title: 'Resumen | Gestión de Soporte' },
  { path: 'casos', component: CasesComponent, title: 'Casos | Gestión de Soporte' },
  { path: 'tasks', component: TasksComponent, title: 'Tareas | Gestión de Soporte' },
  { path: 'productividad', component: ProductivityComponent, title: 'Productividad | Gestión de Soporte' },
  { path: 'integracion', component: IntegrationComponent, title: 'Integración | Gestión de Soporte' },
  { path: '', pathMatch: 'full', redirectTo: 'resumen' },
  { path: '**', redirectTo: 'resumen' }
];
