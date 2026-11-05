import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import { CreateMeeting, Meeting, MeetingListItem, UpdateMeeting } from './models/meeting';

/** Meeting minutes API. Contract: docs/open-issues-meetings-api.md */
@Injectable({ providedIn: 'root' })
export class MeetingService {
    private readonly http = inject(HttpClient);

    list(projectId: number, page: number, pageSize: number): Observable<PagedResult<MeetingListItem>> {
        return this.http.get<PagedResult<MeetingListItem>>(this.url(projectId), { params: { page, pageSize } });
    }

    get(projectId: number, meetingId: number): Observable<Meeting> {
        return this.http.get<Meeting>(`${this.url(projectId)}/${meetingId}`);
    }

    /** Saves the minutes and turns its decisions and actions into open issues, all at once. */
    create(projectId: number, body: CreateMeeting): Observable<Meeting> {
        return this.http.post<Meeting>(this.url(projectId), body, { context: showsFieldErrors() });
    }

    update(projectId: number, meetingId: number, body: UpdateMeeting): Observable<Meeting> {
        return this.http.put<Meeting>(`${this.url(projectId)}/${meetingId}`, body, { context: showsFieldErrors() });
    }

    private url(projectId: number): string {
        return `/api/v1/projects/${projectId}/meetings`;
    }
}
