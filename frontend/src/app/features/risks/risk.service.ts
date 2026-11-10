import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import { Risk, RiskFilter, RiskListItem, RiskMatrix, SaveRisk } from './models/risk';

/** Risks API. Contract: docs/risks-api.md */
@Injectable({ providedIn: 'root' })
export class RiskService {
    private readonly http = inject(HttpClient);

    /** Not closed first, then by score (highest first), then by code. */
    list(projectId: number, filter: RiskFilter, page: number, pageSize: number): Observable<PagedResult<RiskListItem>> {
        let params = new HttpParams().set('page', page).set('pageSize', pageSize);
        if (filter.status) params = params.set('status', filter.status);
        if (filter.category) params = params.set('category', filter.category);
        if (filter.severity) params = params.set('severity', filter.severity);
        if (filter.probability && filter.impact) {
            params = params.set('probability', filter.probability).set('impact', filter.impact);
        }
        return this.http.get<PagedResult<RiskListItem>>(this.url(projectId), { params });
    }

    matrix(projectId: number): Observable<RiskMatrix> {
        return this.http.get<RiskMatrix>(`${this.url(projectId)}/matrix`);
    }

    get(projectId: number, riskId: number): Observable<Risk> {
        return this.http.get<Risk>(`${this.url(projectId)}/${riskId}`);
    }

    /** The form shows 400 field errors itself. */
    create(projectId: number, body: SaveRisk): Observable<Risk> {
        return this.http.post<Risk>(this.url(projectId), body, { context: showsFieldErrors() });
    }

    update(projectId: number, riskId: number, body: SaveRisk): Observable<Risk> {
        return this.http.put<Risk>(`${this.url(projectId)}/${riskId}`, body, { context: showsFieldErrors() });
    }

    private url(projectId: number): string {
        return `/api/v1/projects/${projectId}/risks`;
    }
}
