import { UserSummary } from '../../../shared/models/user-summary';
import { OpenIssueListItem } from '../../open-issues/models/open-issue';

export type MeetingType =
    | 'RequestInterview'
    | 'AnalysisSession'
    | 'KickOff'
    | 'Review'
    | 'Internal'
    | 'CustomerVisit'
    | 'Handover'
    | 'ProjectEvaluation';

export const MEETING_TYPE_LABELS: Record<MeetingType, string> = {
    RequestInterview: 'Talep Görüşmesi',
    AnalysisSession: 'Analiz Oturumu',
    KickOff: 'Proje Başlangıç (Kick-off)',
    Review: 'Gözden Geçirme',
    Internal: 'Departman İçi',
    CustomerVisit: 'Müşteri Ziyareti',
    Handover: 'Devir Görüşmesi',
    ProjectEvaluation: 'Proje Değerlendirme',
};

/** A row of GET /api/v1/projects/{projectId}/meetings (newest first). */
export interface MeetingListItem {
    id: number;
    code: string;
    type: MeetingType;
    date: string;
    participants: string;
    decisionCount: number;
    actionCount: number;
    openActionCount: number;
}

/** The minutes; decisions and actions are open issues, shown with their current status. */
export interface Meeting {
    id: number;
    projectId: number;
    code: string;
    type: MeetingType;
    date: string;
    participants: string;
    agenda: string;
    decisions: OpenIssueListItem[];
    actions: OpenIssueListItem[];
    createdBy: UserSummary;
    createdAt: string;
    updatedAt: string | null;
}

/** PUT body: the minutes only. Decisions and actions are managed on the Open Issues screen. */
export interface UpdateMeeting {
    type: MeetingType;
    date: string;
    participants: string;
    agenda: string;
}

/** An action of new minutes. Send exactly one of ownerUserId and ownerName. */
export interface MeetingAction {
    title: string;
    ownerUserId: number | null;
    ownerName: string | null;
    dueDate: string | null;
}

/** POST body: the decisions and actions become open issues of the project. */
export interface CreateMeeting extends UpdateMeeting {
    decisions: string[];
    actions: MeetingAction[];
}
