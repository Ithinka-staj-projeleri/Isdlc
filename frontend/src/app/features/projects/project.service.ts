import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import { UserLookup, UserRole } from '../../shared/models/user-summary';
import { Project, ProjectFilter, ProjectInput, ProjectListItem } from './project.model';

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

  /** One project with all its details. */
  get(projectId: number): Observable<Project> {
    return this._http.get<Project>(`${this._baseUrl}/${projectId}`);
  }

  /**
   * Opens a new request (only a business analyst can). The form shows 400 field errors itself,
   * so the error interceptor does not also show an alert for them.
   */
  create(input: ProjectInput): Observable<Project> {
    return this._http.post<Project>(this._baseUrl, input, { context: showsFieldErrors() });
  }

  /**
   * Updates the basic information of an active project (409 when it is not active).
   * Status, current step, priority and level are not sent: Process and Assessment own them.
   */
  update(projectId: number, input: ProjectInput): Observable<Project> {
    return this._http.put<Project>(`${this._baseUrl}/${projectId}`, input, { context: showsFieldErrors() });
  }

  /** Active users for pickers, e.g. business analysts. */
  users(role?: UserRole): Observable<UserLookup[]> {
    const params = role ? new HttpParams().set('role', role) : undefined;
    return this._http.get<UserLookup[]>('/api/v1/users', { params });
  }
}
