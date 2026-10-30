import { Injectable } from '@angular/core';

import {
  HttpClient
} from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class AcumaticaService {
  private readonly apiBaseUrl = 'https://localhost:7004/api';
  private readonly apiUrl = `${this.apiBaseUrl}/Acumatica`;

  constructor(
    private http: HttpClient
  ) {
  }

  login(payload: any) {

    return this.http.post(
      `${this.apiUrl}/login`,
      payload,
      {
        withCredentials: true
      }
    );
  }

  synchronize() {
    return this.http.post(
      `${this.apiBaseUrl}/Dashboard/sync`,
      {},
      { withCredentials: true }
    );
  }

  getDashboardView(owner = '') {
    return this.http.get<any>(
      `${this.apiBaseUrl}/Dashboard/view`,
      { withCredentials: true, params: owner ? { owner } : {} }
    );
  }

  getCurrentUser() {
    return this.http.get<any>(`${this.apiUrl}/me`, { withCredentials: true });
  }

  getTeamAccess() {
    return this.http.get<{ canViewTeam: boolean }>(
      `${this.apiBaseUrl}/team-dashboard/access`,
      { withCredentials: true }
    );
  }

  getTeamOwners() {
    return this.http.get<string[]>(
      `${this.apiBaseUrl}/team-dashboard/owners`,
      { withCredentials: true }
    );
  }

  logout() {

    return this.http.post(
      `${this.apiUrl}/logout`,
      {},
      {
        withCredentials: true
      }
    );
  }
}
