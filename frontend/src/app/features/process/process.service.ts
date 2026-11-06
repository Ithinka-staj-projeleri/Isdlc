import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { PagedResult } from '../../shared/models/paged-result';
import { ProcessStage } from './models/process-stage';
import { AnswerStep, ProjectProcess, StepRecord } from './models/project-process';

/**
 * Process API. Contract: docs/process-api.md
 * Every command sends the step the user saw (expectedStepId); if the project has moved on meanwhile, the API answers 409.
 */
@Injectable({ providedIn: 'root' })
export class ProcessService {
    private readonly http = inject(HttpClient);

    /** The 13 stages and their steps, in flow order. Seed data, so it is loaded once per session. */
    readonly stages$: Observable<ProcessStage[]> = this.http
        .get<ProcessStage[]>('/api/v1/process-stages')
        .pipe(shareReplay(1));

    getStages(): Observable<ProcessStage[]> {
        return this.stages$;
    }

    getProjectProcess(projectId: number): Observable<ProjectProcess> {
        return this.http.get<ProjectProcess>(`${this.url(projectId)}/process`);
    }

    /** Newest first. */
    getStepRecords(projectId: number, page: number, pageSize: number): Observable<PagedResult<StepRecord>> {
        return this.http.get<PagedResult<StepRecord>>(`${this.url(projectId)}/step-records`, { params: { page, pageSize } });
    }

    complete(projectId: number, expectedStepId: number, note: string | null): Observable<ProjectProcess> {
        return this.http.post<ProjectProcess>(`${this.url(projectId)}/process/complete`, { expectedStepId, note });
    }

    answer(projectId: number, body: AnswerStep): Observable<ProjectProcess> {
        return this.http.post<ProjectProcess>(`${this.url(projectId)}/process/answer`, body);
    }

    /** Only the Software Department Manager; the reason is required. */
    reject(projectId: number, expectedStepId: number, reason: string): Observable<ProjectProcess> {
        return this.http.post<ProjectProcess>(`${this.url(projectId)}/process/reject`, { expectedStepId, reason });
    }

    private url(projectId: number): string {
        return `/api/v1/projects/${projectId}`;
    }
}
