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

  getSummary() {
    return this.http.get(
      'https://localhost:7004/api/Dashboard/summary',
      { withCredentials: true }
    );
  }

  getCases() {
    return this.http.get(
      'https://localhost:7004/api/Dashboard/cases',
      { withCredentials: true }
    );
  }

  getTasks() {
    return this.http.get(
      'https://localhost:7004/api/Dashboard/tasks',
      { withCredentials: true }
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

  getTrends(period: 'week' | 'month') {
    return this.http.get<any[]>(`https://localhost:7004/api/Dashboard/trends?period=${period}`, { withCredentials: true });
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
