import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay, tap } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import { AddStepInput, ReorderInput, SaveStageInput, SaveStepInput, UpdateFlowInput } from './models/flow-edit';
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

    /** Flows change rarely (only the company administrator edits them), so each is loaded once until edited. */
    private readonly flowCache = new Map<string, Observable<ProcessFlow>>();
    private flowsCache?: Observable<ProcessFlowListItem[]>;

    /** All flows without their steps. */
    get flows$(): Observable<ProcessFlowListItem[]> {
        return (this.flowsCache ??= this.http
            .get<ProcessFlowListItem[]>('/api/v1/process-flows')
            .pipe(tap({ error: () => (this.flowsCache = undefined) }), shareReplay(1)));
    }

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

    // ---- Flow editing (Faz 7 · A7/A8; company administrator). Every write returns the whole flow. ----

    /** The flow with its inactive stages and steps too, for the editor; never cached. */
    getFlowForEditing(code: string): Observable<ProcessFlow> {
        return this.http.get<ProcessFlow>(`${this.flowUrl(code)}`, { params: { includeInactive: true } });
    }

    updateFlow(code: string, input: UpdateFlowInput): Observable<ProcessFlow> {
        return this.write(this.http.put<ProcessFlow>(this.flowUrl(code), input, { context: showsFieldErrors() }));
    }

    copyFlow(code: string, input: { code: string; name: string }): Observable<ProcessFlow> {
        return this.write(this.http.post<ProcessFlow>(`${this.flowUrl(code)}/copy`, input, { context: showsFieldErrors() }));
    }

    reorderFlow(code: string, input: ReorderInput): Observable<ProcessFlow> {
        return this.write(this.http.put<ProcessFlow>(`${this.flowUrl(code)}/order`, input));
    }

    addStage(code: string, input: SaveStageInput): Observable<ProcessFlow> {
        return this.write(this.http.post<ProcessFlow>(`${this.flowUrl(code)}/stages`, input, { context: showsFieldErrors() }));
    }

    updateStage(id: number, input: SaveStageInput): Observable<ProcessFlow> {
        return this.write(this.http.put<ProcessFlow>(`/api/v1/process-stages/${id}`, input, { context: showsFieldErrors() }));
    }

    setStageActive(id: number, active: boolean, rowVersion: string): Observable<ProcessFlow> {
        const action = active ? 'activate' : 'deactivate';
        return this.write(this.http.post<ProcessFlow>(`/api/v1/process-stages/${id}/${action}`, { rowVersion }));
    }

    addStep(code: string, input: AddStepInput): Observable<ProcessFlow> {
        return this.write(this.http.post<ProcessFlow>(`${this.flowUrl(code)}/steps`, input, { context: showsFieldErrors() }));
    }

    updateStep(id: number, input: SaveStepInput): Observable<ProcessFlow> {
        return this.write(this.http.put<ProcessFlow>(`/api/v1/process-steps/${id}`, input, { context: showsFieldErrors() }));
    }

    setStepActive(id: number, active: boolean, rowVersion: string): Observable<ProcessFlow> {
        const action = active ? 'activate' : 'deactivate';
        return this.write(this.http.post<ProcessFlow>(`/api/v1/process-steps/${id}/${action}`, { rowVersion }));
    }

    /** The company's forms, for the "Form tamamlandı" prerequisite. */
    questionSets(): Observable<{ code: string; name: string }[]> {
        return this.http.get<{ code: string; name: string }[]>('/api/v1/question-sets');
    }

    /** After an edit the other screens load the flows again. */
    private write(request: Observable<ProcessFlow>): Observable<ProcessFlow> {
        return request.pipe(tap(() => {
            this.flowCache.clear();
            this.flowsCache = undefined;
        }));
    }

    private flowUrl(code: string): string {
        return `/api/v1/process-flows/${encodeURIComponent(code)}`;
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
