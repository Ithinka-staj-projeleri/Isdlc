import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { AgentStatus, MeetingSuggestions } from './models/meeting-suggestions';
import { RiskAssessmentSuggestion, RiskSuggestions } from './models/risk-suggestions';

const OFF: AgentStatus = { meetingAssistantEnabled: false, riskAssistantEnabled: false };

/**
 * AI assistants (Agents module). The browser only talks to the ISDLC API; the API calls the AI service.
 * Contracts: docs/open-issues-meetings-api.md and docs/risks-api.md → "Yapay zekâ önerileri".
 */
@Injectable({ providedIn: 'root' })
export class AgentService {
    private readonly http = inject(HttpClient);

    /** Asked by each screen that needs it; a failed check counts as "off" and shows nothing. */
    private status(): Observable<AgentStatus> {
        return this.http.get<AgentStatus>('/api/v1/agents/status').pipe(catchError(() => of(OFF)));
    }

    /** True when the AI service is configured and running. */
    meetingAssistantEnabled(): Observable<boolean> {
        return this.status().pipe(map(status => status.meetingAssistantEnabled));
    }

    riskAssistantEnabled(): Observable<boolean> {
        return this.status().pipe(map(status => status.riskAssistantEnabled));
    }

    /** Can take several seconds (a model call). 503 with a Turkish message when the service fails. */
    suggestMeetingItems(projectId: number, meetingDate: string, notes: string): Observable<MeetingSuggestions> {
        return this.http.post<MeetingSuggestions>(
            `/api/v1/projects/${projectId}/meetings/suggestions`,
            { meetingDate, notes },
        );
    }

    /** Three model calls (find, score, preventive action), so it can take a while. */
    suggestRisks(projectId: number, sourceText: string): Observable<RiskSuggestions> {
        return this.http.post<RiskSuggestions>(`/api/v1/projects/${projectId}/risks/suggestions`, { sourceText });
    }

    suggestRiskAssessment(projectId: number, title: string, description: string | null): Observable<RiskAssessmentSuggestion> {
        return this.http.post<RiskAssessmentSuggestion>(
            `/api/v1/projects/${projectId}/risks/assessment-suggestion`,
            { title, description },
        );
    }
}
