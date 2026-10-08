import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { DashboardDataService } from './core/services/dashboard-data.service';
import { LoginComponent } from './features/login/login.component';
import { LucideAngularModule } from 'lucide-angular';
import { ToastNotificationComponent } from './shared/components/toast-notification/toast-notification.component';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, LoginComponent, LucideAngularModule, ToastNotificationComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  readonly data = inject(DashboardDataService);
  private readonly router = inject(Router);
  showLogin = false;
  sidebarCollapsed = localStorage.getItem('radar-sidebar') === 'collapsed';
  profileOpen = false;
  readonly connected$ = this.data.connected$;
  readonly loading$ = this.data.loading$;
  readonly status$ = this.data.status$;
  readonly profile$ = this.data.profile$;
  readonly notification$ = this.data.notification$;

  loginSuccess(profile: any) {
    this.showLogin = false;
    this.data.sessionStarted(profile);
  }

  toggleSidebar() {
    this.sidebarCollapsed = !this.sidebarCollapsed;
    localStorage.setItem('radar-sidebar', this.sidebarCollapsed ? 'collapsed' : 'expanded');
  }

  initials(name: string) {
    return name.split(' ').filter(Boolean).slice(0, 2).map(x => x[0]).join('').toUpperCase() || 'US';
  }

  logout() {
    this.profileOpen = false;
    this.data.disconnect();
  }

  pageTitle() {
    const segment = this.router.url.split('?')[0].split('/')[1] || 'resumen';
    const titles: Record<string, string> = {
      resumen: 'Resumen', casos: 'Casos', tasks: 'Tareas',
      productividad: 'Productividad', integracion: 'Integración'
    };
    return titles[segment] || 'Gestión de soporte';
  }

}
