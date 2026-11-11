import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import {
    RISK_CATEGORY_LABELS, RISK_SEVERITY_LABELS, RISK_SEVERITY_TONES, RISK_STATUS_LABELS, RISK_STATUS_TONES,
    RiskCategory, RiskSeverity, RiskStatus, severityOf,
} from '../models/risk';
import { RiskFormComponent } from '../risk-form/risk-form.component';
import { RiskService } from '../risk.service';
import { AgentService } from '../../agents/agent.service';
import { SuggestedRisk } from '../../agents/models/risk-suggestions';
import { RiskAssistantComponent } from '../risk-assistant/risk-assistant.component';

interface HeatCell {
    probability: number;
    impact: number;
    count: number;
    severity: RiskSeverity;
}

const LEVELS = [1, 2, 3, 4, 5];

/**
 * The project's risks (Risk Analysis Form, flow 4.8): heat map, filters, paging, add / edit.
 * Takes the project id, so the menu page and the project detail page can both show it.
 */
@Component({
    selector: 'app-risk-list',
    imports: [
        FormsModule, EmptyStateComponent, PaginationComponent, StatusBadgeComponent, RiskFormComponent,
        RiskAssistantComponent,
    ],
    templateUrl: './risk-list.component.html',
    styleUrl: './risk-list.component.scss',
})
export class RiskListComponent {
    private readonly riskService = inject(RiskService);

    readonly projectId = input.required<number>();

    /** True when the project is not active: risks can be read but not added or changed. */
    readonly readOnly = input(false);

    protected readonly categoryLabels = RISK_CATEGORY_LABELS;
    protected readonly statusLabels = RISK_STATUS_LABELS;
    protected readonly severityLabels = RISK_SEVERITY_LABELS;
    protected readonly severityTones = RISK_SEVERITY_TONES;
    protected readonly statusTones = RISK_STATUS_TONES;
    protected readonly categories = Object.keys(RISK_CATEGORY_LABELS) as RiskCategory[];
    protected readonly statuses = Object.keys(RISK_STATUS_LABELS) as RiskStatus[];
    protected readonly severities = Object.keys(RISK_SEVERITY_LABELS) as RiskSeverity[];
    protected readonly impacts = LEVELS;

    protected readonly pageSize = 20;
    protected readonly page = signal(1);
    protected readonly category = signal<RiskCategory | ''>('');
    protected readonly status = signal<RiskStatus | ''>('');
    protected readonly severity = signal<RiskSeverity | ''>('');
    /** The heat map cell the list is filtered by; null shows every cell. */
    protected readonly cell = signal<{ probability: number; impact: number } | null>(null);

    /** null: form closed; 'new': adding; a number: editing that risk. */
    protected readonly editing = signal<'new' | number | null>(null);

    /** "Yapay zekâ ile risk öner" is shown only when the AI service is on. */
    protected readonly assistantEnabled = toSignal(inject(AgentService).riskAssistantEnabled(), { initialValue: false });
    protected readonly assistantOpen = signal(false);
    /** The suggestion being added in the form, with its place in the assistant's list. */
    protected readonly fromSuggestion = signal<{ index: number; suggestion: SuggestedRisk } | null>(null);
    /** Suggestions already saved as risks; the assistant marks them. */
    protected readonly addedSuggestions = signal<ReadonlySet<number>>(new Set());

    protected readonly risks = rxResource({
        params: () => ({
            projectId: this.projectId(),
            page: this.page(),
            filter: {
                category: this.category() || undefined,
                status: this.status() || undefined,
                severity: this.severity() || undefined,
                probability: this.cell()?.probability,
                impact: this.cell()?.impact,
            },
        }),
        stream: ({ params }) => this.riskService.list(params.projectId, params.filter, params.page, this.pageSize),
    });

    protected readonly matrix = rxResource({
        params: () => this.projectId(),
        stream: ({ params }) => this.riskService.matrix(params),
    });

    /** Rows from probability 5 down to 1, columns impact 1 to 5, like the paper form. */
    protected readonly heatRows = computed(() => {
        const counts = new Map(
            (this.matrix.value()?.cells ?? []).map(c => [`${c.probability}-${c.impact}`, c.count] as const));

        return [...LEVELS].reverse().map(probability => ({
            probability,
            cells: LEVELS.map((impact): HeatCell => ({
                probability,
                impact,
                count: counts.get(`${probability}-${impact}`) ?? 0,
                severity: severityOf(probability * impact),
            })),
        }));
    });

    protected readonly hasFilter = computed(
        () => !!this.category() || !!this.status() || !!this.severity() || this.cell() !== null);

    /** A new filter shows its results from the first page. */
    protected setCategory(value: RiskCategory | ''): void {
        this.category.set(value);
        this.page.set(1);
    }

    protected setStatus(value: RiskStatus | ''): void {
        this.status.set(value);
        this.page.set(1);
    }

    protected setSeverity(value: RiskSeverity | ''): void {
        this.severity.set(value);
        this.page.set(1);
    }

    /** Clicking the selected cell again clears the cell filter. */
    protected toggleCell(cell: HeatCell): void {
        this.cell.set(this.isSelected(cell) ? null : { probability: cell.probability, impact: cell.impact });
        this.page.set(1);
    }

    protected isSelected(cell: HeatCell): boolean {
        const selected = this.cell();
        return selected?.probability === cell.probability && selected.impact === cell.impact;
    }

    protected onSaved(): void {
        const used = this.fromSuggestion();
        if (used) {
            this.addedSuggestions.update(added => new Set([...added, used.index]));
        }
        this.closeForm();
        this.risks.reload();
        this.matrix.reload();
    }

    protected openAssistant(): void {
        this.addedSuggestions.set(new Set());
        this.assistantOpen.set(true);
    }

    /** Opens the form filled with the suggestion; the assistant waits behind it with its list. */
    protected useSuggestion(used: { index: number; suggestion: SuggestedRisk }): void {
        this.fromSuggestion.set(used);
        this.editing.set('new');
    }

    protected closeForm(): void {
        this.editing.set(null);
        this.fromSuggestion.set(null);
    }
}
