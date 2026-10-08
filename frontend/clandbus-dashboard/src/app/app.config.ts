import {
  ApplicationConfig,
  LOCALE_ID,
  importProvidersFrom,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection
} from '@angular/core';

import {
  provideHttpClient
} from '@angular/common/http';

import {
  provideRouter
} from '@angular/router';

import { routes } from './app.routes';
import { LucideAngularModule, Menu, PanelLeftOpen, LayoutDashboard, Headphones, CircleCheckBig, ChartNoAxesCombined, Network, Clock3, Link2, RefreshCw, LogOut, Hourglass, TrendingUp, TriangleAlert, ChevronUp, X, UserRound, LogIn } from 'lucide-angular';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    provideZoneChangeDetection({
      eventCoalescing: true
    }),

    provideRouter(routes),

    provideHttpClient(),
    { provide: LOCALE_ID, useValue: 'es-MX' },
    importProvidersFrom(LucideAngularModule.pick({ Menu, PanelLeftOpen, LayoutDashboard, Headphones, CircleCheckBig, ChartNoAxesCombined, Network, Clock3, Link2, RefreshCw, LogOut, Hourglass, TrendingUp, TriangleAlert, ChevronUp, X, UserRound, LogIn }))
  ]
};
