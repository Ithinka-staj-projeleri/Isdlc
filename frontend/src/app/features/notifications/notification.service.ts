import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { silentErrors } from '../../core/http/problem-details';
import { PagedResult } from '../../shared/models/paged-result';
import { AppNotification, UnreadCount } from './models/notification';

/**
 * The signed-in user's notifications. Contract: docs/notifications-api.md
 * Keeps the unread count for the bell, so the bell and the notifications page always show the same number.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
    private readonly http = inject(HttpClient);
    private readonly url = '/api/v1/notifications';

    private readonly _unreadCount = signal(0);
    readonly unreadCount = this._unreadCount.asReadonly();

    /** Newest first. */
    list(unreadOnly: boolean, page: number, pageSize: number): Observable<PagedResult<AppNotification>> {
        let params = new HttpParams().set('page', page).set('pageSize', pageSize);
        if (unreadOnly) params = params.set('unreadOnly', true);
        return this.http.get<PagedResult<AppNotification>>(this.url, { params });
    }

    /** Asked in the background (every minute and on page change), so a failure opens no alert. */
    refreshUnreadCount(): void {
        this.http.get<UnreadCount>(`${this.url}/unread-count`, { context: silentErrors() }).subscribe({
            next: result => this._unreadCount.set(result.count),
            error: () => { /* keep the last number; the next refresh tries again */ },
        });
    }

    markRead(notification: AppNotification): Observable<AppNotification> {
        return this.http.post<AppNotification>(`${this.url}/${notification.id}/read`, null).pipe(
            tap(() => {
                if (notification.readAt === null) {
                    this._unreadCount.update(count => Math.max(0, count - 1));
                }
            }),
        );
    }

    markAllRead(): Observable<UnreadCount> {
        return this.http.post<UnreadCount>(`${this.url}/read-all`, null).pipe(tap(() => this._unreadCount.set(0)));
    }
}
