import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { Tenant } from '../models/tenant';
import { TenantFormComponent } from '../tenant-form/tenant-form.component';
import { TenantService } from '../tenant.service';

const PAGE_SIZE = 20;

/**
 * /platform/tenants (Platform Administrator, the only page they see): every company with its user count; open a new
 * one with its first administrator, rename, deactivate. No project or record detail (karar 10).
 */
@Component({
    selector: 'app-tenant-list-page',
    imports: [DatePipe, EmptyStateComponent, PaginationComponent, StatusBadgeComponent, TenantFormComponent],
    templateUrl: './tenant-list-page.component.html',
    styleUrl: './tenant-list-page.component.scss',
})
export class TenantListPageComponent {
    private readonly service = inject(TenantService);

    protected readonly pageSize = PAGE_SIZE;
    protected readonly page = signal(1);
    protected readonly tenants = rxResource({
        params: () => ({ page: this.page() }),
        stream: ({ params }) => this.service.list(params.page, PAGE_SIZE),
    });

    /** undefined: no form; null: new company. */
    protected readonly editing = signal<Tenant | null | undefined>(undefined);
    /** Shown after opening a company, so the administrator's temporary password can be passed on. */
    protected readonly opened = signal<{ tenant: Tenant; adminEmail?: string; temporaryPassword?: string } | null>(null);

    protected onSaved(result: { tenant: Tenant; adminEmail?: string; temporaryPassword?: string }): void {
        const wasNew = this.editing() === null;
        this.editing.set(undefined);
        this.opened.set(wasNew ? result : null);
        this.tenants.reload();
    }
}
