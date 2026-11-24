import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Questionnaire } from '../../shared/question-bank/question-bank.models';

/**
 * Seviye & Uygunluk. The two forms are read and answered with the shared QuestionnaireService ("level", "customer-fit");
 * this service only applies the level form's result to the project. Contract: docs/assessment-api.md
 */
@Injectable({ providedIn: 'root' })
export class AssessmentService {
    private readonly http = inject(HttpClient);

    /** Writes the level form's level and research days to the project; returns the level form. */
    applyLevel(projectId: number): Observable<Questionnaire> {
        return this.http.post<Questionnaire>(`/api/v1/projects/${projectId}/assessment/apply-level`, null);
    }
}
