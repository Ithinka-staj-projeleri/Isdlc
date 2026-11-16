import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** "Açık Konu Var" or "Kısmen Yanıtlandı". */
export type OpenItemMeaning = 'Open' | 'Partial';

/** A Talep Analizi question whose answer still needs something (GET .../request-analysis/open-items). */
export interface RequestAnalysisOpenItem {
    questionId: number;
    no: number;
    section: string | null;
    group: string | null;
    text: string;
    meaning: OpenItemMeaning;
    optionLabel: string;
    /** The stakeholder's answer. */
    answerText: string | null;
    /** The open issue / missing information. */
    note: string | null;
    /** null until it is transferred to Açık Konular. */
    openIssue: { id: number; code: string; status: 'Open' | 'InProgress' | 'Closed' } | null;
}

/**
 * The Talep Analizi tab's own calls (E3). The form itself goes through the shared QuestionnaireService.
 * Contract: docs/question-bank-api.md → "Talep Analizi (E3)".
 */
@Injectable({ providedIn: 'root' })
export class RequestAnalysisService {
    private readonly http = inject(HttpClient);

    openItems(projectId: number): Observable<RequestAnalysisOpenItem[]> {
        return this.http.get<RequestAnalysisOpenItem[]>(`${this.url(projectId)}/open-items`);
    }

    /** Opens a question record in Açık Konular for the answer; once per answer. */
    transfer(projectId: number, questionId: number): Observable<RequestAnalysisOpenItem> {
        return this.http.post<RequestAnalysisOpenItem>(`${this.url(projectId)}/open-items/${questionId}/open-issue`, null);
    }

    private url(projectId: number): string {
        return `/api/v1/projects/${projectId}/request-analysis`;
    }
}
