import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import {
    Improvement,
    ImprovementFilter,
    ImprovementInput,
    ImprovementListItem,
    ImprovementStudy,
    PilotEvaluation,
    PrioritizationInput,
} from './models/process-improvement';

/**
 * Calls to the process improvement API. Contract: docs/process-improvement-api.md
 * Steps, open issues, minutes and history of a proposal use the project screens' services with its id.
 * Forms show 400 field errors themselves, so the error interceptor does not also show an alert for them.
 */
@Injectable({ providedIn: 'root' })
export class ProcessImprovementService {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = '/api/v1/process-improvements';

    list(filter: ImprovementFilter): Observable<PagedResult<ImprovementListItem>> {
        let params = new HttpParams();
        for (const [key, value] of Object.entries(filter)) {
            if (value !== undefined && value !== null && value !== '') {
                params = params.set(key, String(value));
            }
        }
        return this.http.get<PagedResult<ImprovementListItem>>(this.baseUrl, { params });
    }

    get(id: number): Observable<Improvement> {
        return this.http.get<Improvement>(`${this.baseUrl}/${id}`);
    }

    create(input: ImprovementInput): Observable<Improvement> {
        return this.http.post<Improvement>(this.baseUrl, input, { context: showsFieldErrors() });
    }

    update(id: number, input: ImprovementInput): Observable<Improvement> {
        return this.http.put<Improvement>(`${this.baseUrl}/${id}`, input, { context: showsFieldErrors() });
    }

    savePrioritization(id: number, input: PrioritizationInput): Observable<Improvement> {
        return this.http.put<Improvement>(`${this.baseUrl}/${id}/prioritization`, input, { context: showsFieldErrors() });
    }

    saveStudy(id: number, input: ImprovementStudy): Observable<Improvement> {
        return this.http.put<Improvement>(`${this.baseUrl}/${id}/study`, input, { context: showsFieldErrors() });
    }

    savePilotEvaluation(id: number, input: PilotEvaluation): Observable<Improvement> {
        return this.http.put<Improvement>(`${this.baseUrl}/${id}/pilot-evaluation`, input, { context: showsFieldErrors() });
    }
}
