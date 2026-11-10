import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Dashboard, DashboardScope } from './models/dashboard';

/** Dashboard API: every section in one call. Contract: docs/dashboard-api.md */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly _http = inject(HttpClient);

  get(scope: DashboardScope): Observable<Dashboard> {
    const params = scope === 'Mine' ? new HttpParams().set('scope', 'Mine') : undefined;
    return this._http.get<Dashboard>('/api/v1/dashboard', { params });
  }
}
