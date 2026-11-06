import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import { OpenIssue, OpenIssueFilter, OpenIssueListItem, OpenIssueStatus, SaveOpenIssue } from './models/open-issue';

/** Open Issues API. Contract: docs/open-issues-meetings-api.md */
@Injectable({ providedIn: 'root' })
export class OpenIssueService {
    private readonly http = inject(HttpClient);

    /** Not closed first, then by due date, then by code. */
    list(projectId: number, filter: OpenIssueFilter, page: number, pageSize: number): Observable<PagedResult<OpenIssueListItem>> {
        let params = new HttpParams().set('page', page).set('pageSize', pageSize);
        if (filter.status) params = params.set('status', filter.status);
        if (filter.type) params = params.set('type', filter.type);
        if (filter.overdueOnly) params = params.set('overdueOnly', true);
        return this.http.get<PagedResult<OpenIssueListItem>>(this.url(projectId), { params });
    }

    get(projectId: number, openIssueId: number): Observable<OpenIssue> {
        return this.http.get<OpenIssue>(`${this.url(projectId)}/${openIssueId}`);
    }

    /** The form shows 400 field errors itself. */
    create(projectId: number, body: SaveOpenIssue): Observable<OpenIssue> {
        return this.http.post<OpenIssue>(this.url(projectId), body, { context: showsFieldErrors() });
    }

    update(projectId: number, openIssueId: number, body: SaveOpenIssue): Observable<OpenIssue> {
        return this.http.put<OpenIssue>(`${this.url(projectId)}/${openIssueId}`, body, { context: showsFieldErrors() });
    }

    updateStatus(projectId: number, openIssueId: number, status: OpenIssueStatus): Observable<OpenIssue> {
        return this.http.put<OpenIssue>(`${this.url(projectId)}/${openIssueId}/status`, { status });
    }

    private url(projectId: number): string {
        return `/api/v1/projects/${projectId}/open-issues`;
    }
}
