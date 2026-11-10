import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { AcceptanceScores, AssessmentDefinition, LevelAnswers, ProjectAssessment } from './models/assessment';

/** Assessment API (level questions and acceptance scoring). Contract: docs/assessment-api.md */
@Injectable({ providedIn: 'root' })
export class AssessmentService {
    private readonly http = inject(HttpClient);

    /** The same for every project and every user, so it is loaded once. */
    readonly definition$: Observable<AssessmentDefinition> = this.http
        .get<AssessmentDefinition>('/api/v1/assessment/definition')
        .pipe(shareReplay({ bufferSize: 1, refCount: false }));

    get(projectId: number): Observable<ProjectAssessment> {
        return this.http.get<ProjectAssessment>(this.url(projectId));
    }

    /** May be partly answered. Does not change the project's level. */
    saveLevelAnswers(projectId: number, answers: LevelAnswers): Observable<ProjectAssessment> {
        return this.http.put<ProjectAssessment>(`${this.url(projectId)}/level-answers`, answers);
    }

    saveAcceptanceScores(projectId: number, scores: AcceptanceScores): Observable<ProjectAssessment> {
        return this.http.put<ProjectAssessment>(`${this.url(projectId)}/acceptance-scores`, scores);
    }

    /** Writes the suggested level to the project. */
    applyLevel(projectId: number): Observable<ProjectAssessment> {
        return this.http.post<ProjectAssessment>(`${this.url(projectId)}/apply-level`, null);
    }

    private url(projectId: number): string {
        return `/api/v1/projects/${projectId}/assessment`;
    }
}
