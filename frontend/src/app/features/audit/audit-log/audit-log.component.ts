import { Component, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { AUDIT_ACTION_LABELS, AUDIT_ENTITY_LABELS, displayValue, entityLabel, fieldLabel } from '../audit-labels';
import { AuditService } from '../audit.service';
import { AuditEntityType, AuditLog } from '../models/audit-log';

/** Long texts (descriptions, agendas) are cut in the list; the full value is in the tooltip. */
const MAX_VALUE_LENGTH = 160;

/**
 * Kayıt Geçmişi tab of the project detail page: every change to the project and its records,
 * newest first, field by field. Read only; the log is written automatically by the backend.
 */
@Component({
    selector: 'app-audit-log',
    imports: [FormsModule, EmptyStateComponent, PaginationComponent, LocalDatePipe],
    templateUrl: './audit-log.component.html',
    styleUrl: './audit-log.component.scss',
})
export class AuditLogComponent {
    private readonly auditService = inject(AuditService);

    readonly projectId = input.required<number>();

    protected readonly entityTypes = Object.entries(AUDIT_ENTITY_LABELS) as [AuditEntityType, string][];
    protected readonly actionLabels = AUDIT_ACTION_LABELS;
    protected readonly entityLabel = entityLabel;
    protected readonly fieldLabel = fieldLabel;

    protected readonly pageSize = 20;
    protected readonly page = signal(1);
    protected readonly entityType = signal<string>('');

    protected readonly logs = rxResource({
        params: () => ({ projectId: this.projectId(), entityType: this.entityType(), page: this.page() }),
        stream: ({ params }) =>
            this.auditService.projectLog(params.projectId, params.entityType, params.page, this.pageSize),
    });

    protected setEntityType(value: string): void {
        this.entityType.set(value);
        this.page.set(1);
    }

    protected value(log: AuditLog, field: string, value: string | null): string {
        const text = displayValue(log.entityType, field, value);
        return text.length > MAX_VALUE_LENGTH ? text.slice(0, MAX_VALUE_LENGTH) + '…' : text;
    }

    protected fullValue(log: AuditLog, field: string, value: string | null): string | null {
        const text = displayValue(log.entityType, field, value);
        return text.length > MAX_VALUE_LENGTH ? text : null;
    }
}
