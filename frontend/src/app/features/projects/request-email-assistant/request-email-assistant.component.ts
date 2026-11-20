import { Component, inject, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { AgentService } from '../../agents/agent.service';
import { RequestDraft } from '../../agents/models/request-draft';

const MAX_EMAIL = 20_000;

/**
 * "E-postadan doldur" on the new request form: sends the e-mail a request came with and hands the suggested
 * form to the page (drafted). Nothing is saved here. Hidden when the AI service is off.
 * Contract: docs/projects-api.md → "Yapay zekâ: e-postadan talep taslağı".
 */
@Component({
    selector: 'app-request-email-assistant',
    imports: [FormsModule],
    templateUrl: './request-email-assistant.component.html',
    styleUrl: './request-email-assistant.component.scss',
})
export class RequestEmailAssistantComponent {
    private readonly agentService = inject(AgentService);

    readonly drafted = output<RequestDraft>();

    protected readonly enabled = toSignal(this.agentService.requestAssistantEnabled(), { initialValue: false });
    protected readonly maxEmail = MAX_EMAIL;

    protected readonly email = signal('');
    protected readonly busy = signal(false);
    protected readonly result = signal<RequestDraft | null>(null);

    protected ask(): void {
        const email = this.email().trim();
        if (!email || this.busy()) {
            return;
        }

        this.busy.set(true);
        this.agentService.draftRequest(email).subscribe({
            next: draft => {
                this.busy.set(false);
                this.result.set(draft);
                this.drafted.emit(draft);
            },
            // The error interceptor shows the API's message (service busy, quota...).
            error: () => this.busy.set(false),
        });
    }
}
