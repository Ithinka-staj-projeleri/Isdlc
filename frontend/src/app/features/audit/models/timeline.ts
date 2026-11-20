import { UserSummary } from '../../../shared/models/user-summary';
import { MeetingType } from '../../meetings/models/meeting';
import { StepAction } from '../../process/models/project-process';

export type TimelineEventKind = 'Created' | 'Step' | 'Meeting' | 'Decision' | 'RequirementApproved' | 'RiskClosed';

/** GET /api/v1/projects/{projectId}/timeline — one thing that happened on the project. Contract: docs/audit-api.md */
export interface TimelineEvent {
    kind: TimelineEventKind;
    occurredAt: string;
    /** Only the day is known (a meeting): no time is shown. */
    dateOnly: boolean;
    performedBy: UserSummary;
    /** The meeting, open issue, requirement or risk the event is about. */
    recordId: number | null;
    /** The step code for a step, the project code for Created, otherwise the record's code. */
    recordCode: string;
    title: string;
    stepAction: StepAction | null;
    /** A "No" answer that went back to this step. */
    jumpedToStepCode: string | null;
    meetingType: MeetingType | null;
    /** The step's note or the meeting's summary. */
    note: string | null;
}
