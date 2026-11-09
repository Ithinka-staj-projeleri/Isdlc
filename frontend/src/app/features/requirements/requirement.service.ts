import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import {
  Requirement, RequirementFilter, RequirementListItem, RequirementStatus, SaveRequirement,
} from './models/requirement';

/** Requirements API. Contract: docs/requirements-documents-api.md */
@Injectable({ providedIn: 'root' })
export class RequirementService {
  private readonly _http = inject(HttpClient);

  /** Not cancelled first, then by code. Empty filter values are not sent. */
  list(
    projectId: number, filter: RequirementFilter, page: number, pageSize: number,
  ): Observable<PagedResult<RequirementListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    for (const [key, value] of Object.entries(filter)) {
      if (value) {
        params = params.set(key, value);
      }
    }
    return this._http.get<PagedResult<RequirementListItem>>(this.url(projectId), { params });
  }

  get(projectId: number, requirementId: number): Observable<Requirement> {
    return this._http.get<Requirement>(`${this.url(projectId)}/${requirementId}`);
  }

  /** The form shows 400 field errors itself. */
  create(projectId: number, body: SaveRequirement): Observable<Requirement> {
    return this._http.post<Requirement>(this.url(projectId), body, { context: showsFieldErrors() });
  }

  update(projectId: number, requirementId: number, body: SaveRequirement): Observable<Requirement> {
    return this._http.put<Requirement>(`${this.url(projectId)}/${requirementId}`, body, { context: showsFieldErrors() });
  }

  updateStatus(projectId: number, requirementId: number, status: RequirementStatus): Observable<Requirement> {
    return this._http.put<Requirement>(`${this.url(projectId)}/${requirementId}/status`, { status });
  }

  private url(projectId: number): string {
    return `/api/v1/projects/${projectId}/requirements`;
  }
}
