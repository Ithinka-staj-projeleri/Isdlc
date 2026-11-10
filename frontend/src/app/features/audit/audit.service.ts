import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../shared/models/paged-result';
import { AuditLog } from './models/audit-log';

/** Audit API (read only). Contract: docs/audit-api.md */
@Injectable({ providedIn: 'root' })
export class AuditService {
    private readonly http = inject(HttpClient);

    /** Newest first. entityType filters by record kind ("OpenIssue"); empty means all. */
    projectLog(projectId: number, entityType: string, page: number, pageSize: number): Observable<PagedResult<AuditLog>> {
        let params = new HttpParams().set('page', page).set('pageSize', pageSize);
        if (entityType) {
            params = params.set('entityType', entityType);
        }
        return this.http.get<PagedResult<AuditLog>>(`/api/v1/projects/${projectId}/audit-logs`, { params });
    }
}
