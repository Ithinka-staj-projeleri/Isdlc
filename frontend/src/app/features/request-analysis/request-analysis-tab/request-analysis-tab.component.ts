import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { QuestionnaireComponent } from '../../../shared/question-bank/questionnaire/questionnaire.component';
import { OPEN_ISSUE_STATUS_LABELS } from '../../open-issues/models/open-issue';
import { RequestAnalysisOpenItem, RequestAnalysisService } from '../request-analysis.service';

/**
 * Talep Analizi tab (E3): the company's Talep Anlama Soru Listesi filled in during the meetings with the requester
 * (yeni proje 6 / ek geliştirme 4, "Talep anlaşılır"), and the questions still open, each transferred to Açık Konular
 * with one click. Contract: docs/question-bank-api.md → "Talep Analizi (E3)".
 */
@Component({
    selector: 'app-request-analysis-tab',
    imports: [QuestionnaireComponent, StatusBadgeComponent, RouterLink],
    templateUrl: './request-analysis-tab.component.html',
    styleUrl: './request-analysis-tab.component.scss',
})
export class RequestAnalysisTabComponent {
    private readonly requestAnalysisService = inject(RequestAnalysisService);

    readonly projectId = input.required<number>();
    /** The project is not active: the form and the list are read only. */
    readonly readOnly = input(false);

    /** The form's sections in the company's words ("1-Temel" → "Temel sorular"). */
    protected readonly sectionTitles = {
        '1-Temel': '1 · Temel sorular',
        '2-Kapsam': '2 · Kapsam',
        '3-Değerlendirme': '3 · Değerlendirme',
    };
    protected readonly issueStatusLabels = OPEN_ISSUE_STATUS_LABELS;

    protected readonly openItems = rxResource({
        params: () => ({ projectId: this.projectId() }),
        stream: ({ params }) => this.requestAnalysisService.openItems(params.projectId),
    });

    protected readonly items = computed(() => (this.openItems.hasValue() ? this.openItems.value() : []));
    protected readonly waiting = computed(() => this.items().filter(i => !i.openIssue).length);

    /** The question being transferred, so its button cannot be clicked twice. */
    protected readonly transferring = signal<number | null>(null);

    /** Every saved answer can add or remove an open item. */
    protected onFormChanged(): void {
        this.openItems.reload();
    }

    protected transfer(item: RequestAnalysisOpenItem): void {
        this.transferring.set(item.questionId);
        this.requestAnalysisService.transfer(this.projectId(), item.questionId).subscribe({
            next: updated => {
                this.transferring.set(null);
                this.openItems.update(items => items?.map(i => (i.questionId === updated.questionId ? updated : i)));
            },
            // The error interceptor shows why (e.g. transferred meanwhile); the list is loaded again.
            error: () => {
                this.transferring.set(null);
                this.openItems.reload();
            },
        });
    }
}
