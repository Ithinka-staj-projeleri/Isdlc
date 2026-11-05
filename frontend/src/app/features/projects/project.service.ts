import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../shared/models/paged-result';
import { UserLookup, UserRole } from '../../shared/models/user-summary';
import { ProjectFilter, ProjectListItem } from './project.model';

/** Calls to the Projects API. Screens never call HttpClient directly. Contract: docs/projects-api.md */
@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly _http = inject(HttpClient);
  private readonly _baseUrl = '/api/v1/projects';

  /** Filtered, paged list; newest request first. Empty filter values are not sent. */
  list(filter: ProjectFilter): Observable<PagedResult<ProjectListItem>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this._http.get<PagedResult<ProjectListItem>>(this._baseUrl, { params });
  }

  /** Active users for pickers, e.g. business analysts. */
  users(role?: UserRole): Observable<UserLookup[]> {
    const params = role ? new HttpParams().set('role', role) : undefined;
    return this._http.get<UserLookup[]>('/api/v1/users', { params });
  }
}
