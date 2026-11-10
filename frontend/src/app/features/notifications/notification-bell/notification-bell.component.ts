import { Component, DestroyRef, ElementRef, HostListener, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { filter, interval } from 'rxjs';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { AppNotification } from '../models/notification';
import { NotificationService } from '../notification.service';

/** How often the unread count is asked (docs/notifications-api.md → decision 5). */
const REFRESH_MS = 60_000;

/** The 10 newest notifications in the popup; the rest are on /notifications. */
const POPUP_SIZE = 10;

/**
 * The bell in the top bar: unread count, and a popup with the newest notifications.
 * Clicking a notification marks it read and opens its project.
 */
@Component({
    selector: 'app-notification-bell',
    imports: [RouterLink, LocalDatePipe],
    templateUrl: './notification-bell.component.html',
    styleUrl: './notification-bell.component.scss',
})
export class NotificationBellComponent implements OnInit {
    protected readonly notifications = inject(NotificationService);
    private readonly router = inject(Router);
    private readonly host = inject(ElementRef<HTMLElement>);
    private readonly destroyRef = inject(DestroyRef);

    protected readonly open = signal(false);
    protected readonly loading = signal(false);
    protected readonly items = signal<AppNotification[]>([]);

    ngOnInit(): void {
        this.notifications.refreshUnreadCount();

        interval(REFRESH_MS)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe(() => this.notifications.refreshUnreadCount());

        // Something done on the previous page may have notified this user too (e.g. a role they have).
        this.router.events
            .pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef))
            .subscribe(() => {
                this.open.set(false);
                this.notifications.refreshUnreadCount();
            });
    }

    protected toggle(): void {
        if (this.open()) {
            this.open.set(false);
            return;
        }

        this.open.set(true);
        this.loading.set(true);
        this.notifications.list(false, 1, POPUP_SIZE).subscribe({
            next: result => {
                this.items.set(result.items);
                this.loading.set(false);
            },
            error: () => this.loading.set(false),
        });
        this.notifications.refreshUnreadCount();
    }

    protected openNotification(notification: AppNotification): void {
        this.notifications.markRead(notification).subscribe({
            next: () => this.router.navigate(['/projects', notification.project.id]),
            // Still open the project; the notification stays unread and can be marked later.
            error: () => this.router.navigate(['/projects', notification.project.id]),
        });
    }

    protected markAllRead(): void {
        this.notifications.markAllRead().subscribe(() => {
            const now = new Date().toISOString();
            this.items.update(items => items.map(n => (n.readAt ? n : { ...n, readAt: now })));
        });
    }

    /** A click outside the bell closes the popup. */
    @HostListener('document:click', ['$event'])
    protected onDocumentClick(event: MouseEvent): void {
        if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
            this.open.set(false);
        }
    }

    @HostListener('document:keydown.escape')
    protected onEscape(): void {
        this.open.set(false);
    }
}
