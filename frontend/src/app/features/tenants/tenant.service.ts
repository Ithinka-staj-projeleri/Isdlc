import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import { AiUsageSummary, CreateTenantInput, CurrentTenant, Tenant, TenantSettings, UpdateTenantInput } from './models/tenant';

/**
 * Companies (Faz 6). List, opening and editing are the Platform Administrator's; the settings are the company
 * administrator's. Contract: docs/multi-tenancy.md → Uç noktalar.
 */
@Injectable({ providedIn: 'root' })
export class TenantService {
    private readonly http = inject(HttpClient);
    private readonly url = '/api/v1/tenants';

    /** Every company, inactive ones too, by name. */
    list(page: number, pageSize: number): Observable<PagedResult<Tenant>> {
        const params = new HttpParams().set('page', page).set('pageSize', pageSize);
        return this.http.get<PagedResult<Tenant>>(this.url, { params });
    }

    create(input: CreateTenantInput): Observable<Tenant> {
        return this.http.post<Tenant>(this.url, input, { context: showsFieldErrors() });
    }

    update(id: number, input: UpdateTenantInput): Observable<Tenant> {
        return this.http.put<Tenant>(`${this.url}/${id}`, input, { context: showsFieldErrors() });
    }

    /** The signed-in user's company with its active administrators (for "whom to ask"). */
    current(): Observable<CurrentTenant> {
        return this.http.get<CurrentTenant>(`${this.url}/current`);
    }

    getSettings(): Observable<TenantSettings> {
        return this.http.get<TenantSettings>(`${this.url}/current/settings`);
    }

    updateSettings(settings: TenantSettings): Observable<TenantSettings> {
        return this.http.put<TenantSettings>(`${this.url}/current/settings`, settings, { context: showsFieldErrors() });
    }

    /** This month when month is not given. */
    aiUsage(month?: string): Observable<AiUsageSummary> {
        const params = month ? new HttpParams().set('month', month) : undefined;
        return this.http.get<AiUsageSummary>(`${this.url}/current/ai-usage`, { params });
    }

    /** The models the company can choose; empty while the AI service is off. */
    aiModels(): Observable<string[]> {
        return this.http.get<{ models: string[] }>('/api/v1/agents/models').pipe(map(r => r.models));
    }
}
