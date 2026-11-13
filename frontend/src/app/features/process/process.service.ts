import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay, tap } from 'rxjs';
import { PagedResult } from '../../shared/models/paged-result';
import { ProcessFlow, ProcessFlowListItem } from './models/process-stage';
import { AnswerStep, ProjectProcess, StepRecord } from './models/project-process';
import { RequestDecision, RequestDecisionForm } from './models/request-decision';

/**
 * Process API. Contract: docs/process-api.md
 * Every command sends the step the user saw (expectedStepId); if the project has moved on meanwhile, the API answers 409.
 */
@Injectable({ providedIn: 'root' })
export class ProcessService {
    private readonly http = inject(HttpClient);

    /** Flows are seed data, so each is loaded once per session. */
    private readonly flowCache = new Map<string, Observable<ProcessFlow>>();

    /** All flows without their steps. */
    readonly flows$: Observable<ProcessFlowListItem[]> = this.http
        .get<ProcessFlowListItem[]>('/api/v1/process-flows')
        .pipe(shareReplay(1));

    /** A flow with its stages and steps, in flow order. */
    getFlow(code: string): Observable<ProcessFlow> {
        let flow = this.flowCache.get(code);
        if (!flow) {
            flow = this.http.get<ProcessFlow>(`/api/v1/process-flows/${encodeURIComponent(code)}`).pipe(
                // A failed load is not kept, so the next screen tries again.
                tap({ error: () => this.flowCache.delete(code) }),
                shareReplay(1),
            );
            this.flowCache.set(code, flow);
        }
        return flow;
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

    /** Only the Software Department Manager; with the Talep Ret / Erteleme Formu. */
    reject(projectId: number, expectedStepId: number, decision: RequestDecisionForm): Observable<ProjectProcess> {
        return this.http.post<ProjectProcess>(`${this.url(projectId)}/process/reject`, { expectedStepId, decision });
    }

    /** Only the Software Department Manager, only a request; it waits on its step until taken back. */
    postpone(
        projectId: number, expectedStepId: number, decision: RequestDecisionForm, reevaluationDate: string,
    ): Observable<ProjectProcess> {
        return this.http.post<ProjectProcess>(`${this.url(projectId)}/process/postpone`, { expectedStepId, decision, reevaluationDate });
    }

    /** Only the Software Department Manager: a postponed request continues on its step. */
    resume(projectId: number, expectedStepId: number, note: string | null): Observable<ProjectProcess> {
        return this.http.post<ProjectProcess>(`${this.url(projectId)}/process/resume`, { expectedStepId, note });
    }

    /** The project's Talep Ret / Erteleme Formları, newest first. */
    getRequestDecisions(projectId: number): Observable<RequestDecision[]> {
        return this.http.get<RequestDecision[]>(`${this.url(projectId)}/request-decisions`);
    }

    private url(projectId: number): string {
        return `/api/v1/projects/${projectId}`;
    }
}
