import { Component, input } from '@angular/core';
import { DateOnlyPipe } from '../../../shared/pipes/date-only.pipe';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { DECISION_REASON_LABELS, REQUEST_DECISION_LABELS, RequestDecision } from '../models/request-decision';

/** A project's filled-in Talep Ret / Erteleme Formları, newest first, with the form's field names. */
@Component({
    selector: 'app-request-decision-list',
    imports: [DateOnlyPipe, LocalDatePipe],
    templateUrl: './request-decision-list.component.html',
    styleUrl: './request-decision-list.component.scss',
})
export class RequestDecisionListComponent {
    readonly decisions = input.required<RequestDecision[]>();

    protected readonly decisionLabels = REQUEST_DECISION_LABELS;
    protected readonly reasonLabels = DECISION_REASON_LABELS;
}
