import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { PROJECT_STATUS_LABELS, ProjectStatus } from '../../../shared/models/project-status';
import { PROJECT_STATUS_TONES } from '../../projects/project-labels';
import { ImprovementFormComponent } from '../improvement-form/improvement-form.component';
import {
    IMPROVEMENT_PRIORITY_LABELS,
    IMPROVEMENT_PRIORITY_TONES,
    IMPROVEMENT_SOURCE_LABELS,
    IMPROVEMENT_STAGES,
    Improvement,
} from '../models/process-improvement';
import { ProcessImprovementService } from '../process-improvement.service';

const PAGE_SIZE = 20;

/** Menu → Süreç İyileştirme: the proposals, newest first, with "+ Yeni öneri". */
@Component({
    selector: 'app-improvement-list',
    imports: [DatePipe, RouterLink, EmptyStateComponent, PaginationComponent, StatusBadgeComponent, ImprovementFormComponent],
    templateUrl: './improvement-list.component.html',
    styleUrl: './improvement-list.component.scss',
})
export class ImprovementListComponent {
    private readonly service = inject(ProcessImprovementService);
    private readonly router = inject(Router);

    protected readonly sourceLabels = IMPROVEMENT_SOURCE_LABELS;
    protected readonly priorityLabels = IMPROVEMENT_PRIORITY_LABELS;
    protected readonly priorityTones = IMPROVEMENT_PRIORITY_TONES;
    protected readonly statusLabels = PROJECT_STATUS_LABELS;
    protected readonly statusTones = PROJECT_STATUS_TONES;
    protected readonly stages = IMPROVEMENT_STAGES;
    protected readonly pageSize = PAGE_SIZE;

    /** Proposals end as Closed (standardized) or Rejected; the others do not happen on this flow. */
    protected readonly statusOptions: { value: ProjectStatus | ''; label: string }[] = [
        { value: 'Active', label: 'Devam edenler' },
        { value: '', label: 'Tüm öneriler' },
        { value: 'Closed', label: 'Kapandı' },
        { value: 'Rejected', label: 'Reddedildi' },
    ];

    protected readonly status = signal<ProjectStatus | ''>('Active');
    protected readonly stageOrder = signal<number | null>(null);
    protected readonly page = signal(1);
    protected readonly creating = signal(false);

    protected readonly improvements = rxResource({
        params: () => ({ status: this.status(), stageOrder: this.stageOrder(), page: this.page() }),
        stream: ({ params }) => this.service.list({
            status: params.status || undefined,
            stageOrder: params.stageOrder ?? undefined,
            page: params.page,
            pageSize: PAGE_SIZE,
        }),
    });

    protected onStatusChange(value: string): void {
        this.status.set(value as ProjectStatus | '');
        this.page.set(1);
    }

    protected onStageChange(value: string): void {
        this.stageOrder.set(value ? Number(value) : null);
        this.page.set(1);
    }

    protected open(id: number): void {
        this.router.navigate(['/process-improvements', id]);
    }

    protected onCreated(improvement: Improvement): void {
        this.creating.set(false);
        this.open(improvement.id);
    }
}
