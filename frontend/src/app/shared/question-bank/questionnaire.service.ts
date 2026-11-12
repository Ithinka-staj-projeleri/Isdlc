import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Questionnaire, SaveAnswer, SaveQuestionnaire } from './question-bank.models';

/** A project's forms (level, request analysis, risk analysis, internal approval). Contract: docs/question-bank-api.md */
@Injectable({ providedIn: 'root' })
export class QuestionnaireService {
  private readonly http = inject(HttpClient);

  /** The form with its questions, answers and summary; empty answers when nothing was saved yet. */
  get(projectId: number, setCode: string): Observable<Questionnaire> {
    return this.http.get<Questionnaire>(this.url(projectId, setCode));
  }

  /** The header: subcategory, participants, date, approver. */
  save(projectId: number, setCode: string, header: SaveQuestionnaire): Observable<Questionnaire> {
    return this.http.put<Questionnaire>(this.url(projectId, setCode), header);
  }

  /** One answer; every field null empties it. The whole form comes back, because the summary changes. */
  saveAnswer(projectId: number, setCode: string, questionId: number, answer: SaveAnswer): Observable<Questionnaire> {
    return this.http.put<Questionnaire>(`${this.url(projectId, setCode)}/answers/${questionId}`, answer);
  }

  private url(projectId: number, setCode: string): string {
    return `/api/v1/projects/${projectId}/questionnaires/${encodeURIComponent(setCode)}`;
  }
}
