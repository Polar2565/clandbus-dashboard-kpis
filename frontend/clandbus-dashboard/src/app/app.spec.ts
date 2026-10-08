import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { importProvidersFrom } from '@angular/core';
import { LucideAngularModule, LayoutDashboard, Headphones, CircleCheckBig, ChartNoAxesCombined, Network, RefreshCw, LogOut, ChevronUp, UserRound, LogIn } from 'lucide-angular';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        importProvidersFrom(LucideAngularModule.pick({ LayoutDashboard, Headphones, CircleCheckBig, ChartNoAxesCombined, Network, RefreshCw, LogOut, ChevronUp, UserRound, LogIn }))],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the application context', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.page-context')?.textContent).toContain('GESTIÓN DE SOPORTE');
  });
});
