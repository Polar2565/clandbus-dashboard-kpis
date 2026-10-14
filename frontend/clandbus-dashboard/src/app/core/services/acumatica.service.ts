import { Injectable } from '@angular/core';

import {
  HttpClient
} from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class AcumaticaService {

  private apiUrl =
    'https://localhost:7004/api/Acumatica';

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

  getOrders() {

    return this.http.get(
      `${this.apiUrl}/orders`,
      {
        withCredentials: true
      }
    );
  }

  synchronize() {
    return this.http.post(
      'https://localhost:7004/api/Dashboard/sync',
      {},
      { withCredentials: true }
    );
  }

  getSummary(owner = '') {
    return this.http.get(
      'https://localhost:7004/api/Dashboard/summary',
      { withCredentials: true, params: owner ? { owner } : {} }
    );
  }

  getCases(owner = '') {
    return this.http.get(
      'https://localhost:7004/api/Dashboard/cases',
      { withCredentials: true, params: owner ? { owner } : {} }
    );
  }

  getTasks(owner = '') {
    return this.http.get(
      'https://localhost:7004/api/Dashboard/tasks',
      { withCredentials: true, params: owner ? { owner } : {} }
    );
  }

  importTasks(file: File) {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<any>(
      'https://localhost:7004/api/Dashboard/import-tasks',
      form,
      { withCredentials: true }
    );
  }

  importLatestTasks() {
    return this.http.post<any>(
      'https://localhost:7004/api/Dashboard/import-latest-tasks',
      {},
      { withCredentials: true }
    );
  }

  getCurrentUser() {
    return this.http.get<any>(`${this.apiUrl}/me`, { withCredentials: true });
  }

  getTrends(period: 'week' | 'month', owner = '') {
    return this.http.get<any[]>('https://localhost:7004/api/Dashboard/trends', {
      withCredentials: true,
      params: owner ? { period, owner } : { period }
    });
  }

  getTeamAccess() {
    return this.http.get<{ canViewTeam: boolean }>(
      'https://localhost:7004/api/team-dashboard/access',
      { withCredentials: true }
    );
  }

  getTeamOwners() {
    return this.http.get<string[]>(
      'https://localhost:7004/api/team-dashboard/owners',
      { withCredentials: true }
    );
  }

  updateOrder(payload: any) {

    return this.http.post(
      `${this.apiUrl}/update-order`,
      payload,
      {
        withCredentials: true
      }
    );
  }

  removeHold(payload: any) {

    return this.http.post(
      `${this.apiUrl}/remove-hold`,
      payload,
      {
        withCredentials: true
      }
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
