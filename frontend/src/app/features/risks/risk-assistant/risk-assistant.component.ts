import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AgentService } from '../../agents/agent.service';
import { RiskSuggestions, SuggestedRisk } from '../../agents/models/risk-suggestions';
import { RISK_CATEGORY_LABELS, RISK_SEVERITY_LABELS, RISK_SEVERITY_TONES } from '../models/risk';

const MAX_TEXT = 20_000;

/**
 * "Yapay zekâ ile risk öner": finds risks in a text (meeting notes, an e-mail...), each with a suggested category,
 * score and preventive action. Nothing is saved here: "Forma aktar" opens the risk form filled with the suggestion,
 * and the person saves it. The list stays while the form is open, so several risks can be added one after another.
 */
@Component({
    selector: 'app-risk-assistant',
    imports: [FormsModule, StatusBadgeComponent],
    templateUrl: './risk-assistant.component.html',
    styleUrl: './risk-assistant.component.scss',
})
export class RiskAssistantComponent {
    private readonly agentService = inject(AgentService);

    readonly projectId = input.required<number>();
    /** Suggestions already saved as risks (by their place in the list); marked "Eklendi". */
    readonly added = input<ReadonlySet<number>>(new Set());

    readonly use = output<{ index: number; suggestion: SuggestedRisk }>();
    readonly closed = output<void>();

    protected readonly categoryLabels = RISK_CATEGORY_LABELS;
    protected readonly severityLabels = RISK_SEVERITY_LABELS;
    protected readonly severityTones = RISK_SEVERITY_TONES;
    protected readonly maxText = MAX_TEXT;

    protected readonly text = signal('');
    protected readonly busy = signal(false);
    protected readonly result = signal<RiskSuggestions | null>(null);

    protected ask(): void {
        const text = this.text().trim();
        if (!text || this.busy()) {
            return;
        }

        this.busy.set(true);
        this.agentService.suggestRisks(this.projectId(), text).subscribe({
            next: suggestions => {
                this.busy.set(false);
                this.result.set(suggestions);
            },
            // The error interceptor shows the API's message (service busy, quota...).
            error: () => this.busy.set(false),
        });
    }
}
