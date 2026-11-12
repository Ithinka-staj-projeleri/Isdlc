import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { PROJECT_STATUS_LABELS } from '../../../shared/models/project-status';
import { AuditLogComponent } from '../../audit/audit-log/audit-log.component';
import { MeetingListComponent } from '../../meetings/meeting-list/meeting-list.component';
import { OpenIssueListComponent } from '../../open-issues/open-issue-list/open-issue-list.component';
import { ProjectProcessComponent } from '../../process/project-process/project-process.component';
import { PROJECT_STATUS_TONES } from '../../projects/project-labels';
import { ImprovementFormComponent } from '../improvement-form/improvement-form.component';
import { ImprovementSectionComponent } from '../improvement-section/improvement-section.component';
import {
    IMPROVEMENT_PRIORITY_LABELS,
    IMPROVEMENT_PRIORITY_TONES,
    IMPROVEMENT_SOURCE_LABELS,
    Improvement,
} from '../models/process-improvement';
import { ProcessImprovementService } from '../process-improvement.service';

type DetailTab = 'proposal' | 'prioritization' | 'study' | 'pilot' | 'process' | 'open-issues' | 'meetings' | 'history';

/**
 * A process improvement proposal: a summary on top and one tab per section. Process, open issues, minutes and
 * history are the project screens' components: a proposal is a project record on its own flow.
 * The chosen tab is kept in the URL (?tab=study).
 */
@Component({
    selector: 'app-improvement-detail',
    imports: [
        RouterLink,
        LocalDatePipe,
        EmptyStateComponent,
        StatusBadgeComponent,
        ImprovementFormComponent,
        ImprovementSectionComponent,
        ProjectProcessComponent,
        OpenIssueListComponent,
        MeetingListComponent,
        AuditLogComponent,
    ],
    templateUrl: './improvement-detail.component.html',
    styleUrl: './improvement-detail.component.scss',
})
export class ImprovementDetailComponent {
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly service = inject(ProcessImprovementService);

    protected readonly statusLabels = PROJECT_STATUS_LABELS;
    protected readonly statusTones = PROJECT_STATUS_TONES;
    protected readonly sourceLabels = IMPROVEMENT_SOURCE_LABELS;
    protected readonly priorityLabels = IMPROVEMENT_PRIORITY_LABELS;
    protected readonly priorityTones = IMPROVEMENT_PRIORITY_TONES;

    protected readonly tabs: { key: DetailTab; label: string }[] = [
        { key: 'proposal', label: 'Öneri' },
        { key: 'prioritization', label: 'Önceliklendirme' },
        { key: 'study', label: 'Çalışma' },
        { key: 'pilot', label: 'Pilot' },
        { key: 'process', label: 'Süreç' },
        { key: 'open-issues', label: 'Açık Konular' },
        { key: 'meetings', label: 'Toplantı Tutanakları' },
        { key: 'history', label: 'Kayıt Geçmişi' },
    ];

    protected readonly id = toSignal(this.route.paramMap.pipe(map(params => Number(params.get('id')))), { requireSync: true });

    protected readonly tab = toSignal(
        this.route.queryParamMap.pipe(map(params => (params.get('tab') as DetailTab | null) ?? 'proposal')),
        { requireSync: true },
    );

    protected readonly improvement = rxResource({
        params: () => ({ id: this.id() }),
        stream: ({ params }) => this.service.get(params.id),
    });

    /** The proposal when loaded. Reading a resource's value in the error state throws, so it is checked first. */
    protected readonly loaded = computed(() => (this.improvement.hasValue() ? this.improvement.value() : undefined));

    protected readonly notFound = computed(() => {
        const error = this.improvement.error();
        const cause = (error as { cause?: unknown } | undefined)?.cause ?? error;
        return cause instanceof HttpErrorResponse && cause.status === 404;
    });

    /** A closed or rejected proposal keeps its information as it was. */
    protected readonly readOnly = computed(() => this.loaded()?.status !== 'Active');

    protected readonly editing = signal(false);

    protected selectTab(tab: DetailTab): void {
        this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { tab: tab === 'proposal' ? null : tab },
            queryParamsHandling: 'merge',
        });
    }

    /** A saved section or the edited proposal replaces the shown one without reloading. */
    protected onSaved(improvement: Improvement): void {
        this.editing.set(false);
        this.improvement.set(improvement);
    }
}
