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

/** Someone at the meeting; role is free text (role, unit or company). */
export interface MeetingParticipant {
    name: string;
    role: string | null;
}

/** A row of GET /api/v1/projects/{projectId}/meetings (newest first). */
export interface MeetingListItem {
    id: number;
    code: string;
    type: MeetingType;
    /** The departure date of a visit. */
    date: string;
    /** Visits only. */
    returnDate: string | null;
    /** "Elif Yılmaz, Can Öztürk" */
    participantNames: string;
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
    returnDate: string | null;
    recordedBy: UserSummary;
    participants: MeetingParticipant[];
    agenda: string;
    summary: string | null;
    completedStages: string | null;
    ongoingStages: string | null;
    nextSteps: string | null;
    plannedActivities: string | null;
    completedActivities: string | null;
    decisions: OpenIssueListItem[];
    actions: OpenIssueListItem[];
    createdBy: UserSummary;
    createdAt: string;
    updatedAt: string | null;
}

/**
 * PUT body: the minutes only. Decisions and actions are managed on the Open Issues screen.
 * returnDate and the activities only for a customer visit (null otherwise).
 */
export interface UpdateMeeting {
    type: MeetingType;
    date: string;
    returnDate: string | null;
    recordedByUserId: number;
    participants: MeetingParticipant[];
    agenda: string;
    summary: string | null;
    completedStages: string | null;
    ongoingStages: string | null;
    nextSteps: string | null;
    plannedActivities: string | null;
    completedActivities: string | null;
}

/** A customer visit has its own fields (return date, activities); other minutes do not. */
export const VISIT_TYPE: MeetingType = 'CustomerVisit';

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
