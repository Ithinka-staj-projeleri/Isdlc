import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { LocalDatePipe } from '../../shared/pipes/local-date-pipe';
import { AppNotification } from './models/notification';
import { NotificationService } from './notification.service';

/** All of the signed-in user's notifications ("Tümünü gör" in the bell). */
@Component({
    selector: 'app-notifications-page',
    imports: [FormsModule, EmptyStateComponent, PaginationComponent, LocalDatePipe],
    templateUrl: './notifications-page.component.html',
    styleUrl: './notifications-page.component.scss',
})
export class NotificationsPageComponent {
    protected readonly notifications = inject(NotificationService);
    private readonly router = inject(Router);

    protected readonly pageSize = 20;
    protected readonly page = signal(1);
    protected readonly unreadOnly = signal(false);

    protected readonly list = rxResource({
        params: () => ({ page: this.page(), unreadOnly: this.unreadOnly() }),
        stream: ({ params }) => this.notifications.list(params.unreadOnly, params.page, this.pageSize),
    });

    protected setUnreadOnly(value: boolean): void {
        this.unreadOnly.set(value);
        this.page.set(1);
    }

    protected openNotification(notification: AppNotification): void {
        this.notifications.markRead(notification).subscribe({
            next: () => this.router.navigate(['/projects', notification.project.id]),
            error: () => this.router.navigate(['/projects', notification.project.id]),
        });
    }

    protected markAllRead(): void {
        this.notifications.markAllRead().subscribe(() => this.list.reload());
    }
}
