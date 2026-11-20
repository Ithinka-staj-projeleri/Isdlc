import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { AgentStatus, MeetingSuggestions, MeetingTranscript } from './models/meeting-suggestions';
import { RequestDraft } from './models/request-draft';
import { RiskAssessmentSuggestion, RiskSuggestions } from './models/risk-suggestions';

const OFF: AgentStatus = {
    meetingAssistantEnabled: false,
    riskAssistantEnabled: false,
    requestAssistantEnabled: false,
    transcriptionEnabled: false,
};

/**
 * AI assistants (Agents module). The browser only talks to the ISDLC API; the API calls the AI service.
 * Contracts: docs/open-issues-meetings-api.md and docs/risks-api.md → "Yapay zekâ önerileri";
 * docs/projects-api.md → "Yapay zekâ: e-postadan talep taslağı".
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

    transcriptionEnabled(): Observable<boolean> {
        return this.status().pipe(map(status => status.meetingAssistantEnabled && status.transcriptionEnabled));
    }

    /** A recording turned into text on the AI service's machine. Can take minutes; 400 when the file is not audio. */
    transcribeMeeting(projectId: number, recording: File): Observable<MeetingTranscript> {
        const body = new FormData();
        body.append('file', recording, recording.name);
        return this.http.post<MeetingTranscript>(`/api/v1/projects/${projectId}/meetings/transcript`, body);
    }

    requestAssistantEnabled(): Observable<boolean> {
        return this.status().pipe(map(status => status.requestAssistantEnabled));
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

    /** The new request form filled from an e-mail; nothing is saved. Only a Business Analyst may ask (403 otherwise). */
    draftRequest(emailText: string): Observable<RequestDraft> {
        return this.http.post<RequestDraft>('/api/v1/agents/request-draft', { emailText });
    }
}
