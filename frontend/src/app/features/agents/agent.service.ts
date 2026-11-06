import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { AgentStatus, MeetingSuggestions } from './models/meeting-suggestions';

/**
 * AI assistants (Agents module). The browser only talks to the ISDLC API; the API calls the AI service.
 * Contract: docs/open-issues-meetings-api.md → "Yapay zekâ önerileri".
 */
@Injectable({ providedIn: 'root' })
export class AgentService {
    private readonly http = inject(HttpClient);

    /** True when the AI service is configured and running. A failed check counts as "off" and shows nothing. */
    meetingAssistantEnabled(): Observable<boolean> {
        return this.http.get<AgentStatus>('/api/v1/agents/status').pipe(
            map(status => status.meetingAssistantEnabled),
            catchError(() => of(false)),
        );
    }

    /** Can take several seconds (a model call). 503 with a Turkish message when the service fails. */
    suggestMeetingItems(projectId: number, meetingDate: string, notes: string): Observable<MeetingSuggestions> {
        return this.http.post<MeetingSuggestions>(
            `/api/v1/projects/${projectId}/meetings/suggestions`,
            { meetingDate, notes },
        );
    }
}
