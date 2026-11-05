import { UserSummary } from '../../../shared/models/user-summary';

export type OpenIssueType = 'Question' | 'Action' | 'Decision' | 'ChangeRequest' | 'Dependency' | 'Assumption';
export type OpenIssueStatus = 'Open' | 'InProgress' | 'Closed';

export const OPEN_ISSUE_TYPE_LABELS: Record<OpenIssueType, string> = {
    Question: 'Soru',
    Action: 'Aksiyon',
    Decision: 'Karar',
    ChangeRequest: 'Değişiklik Talebi',
    Dependency: 'Bağımlılık',
    Assumption: 'Varsayım',
};

export const OPEN_ISSUE_STATUS_LABELS: Record<OpenIssueStatus, string> = {
    Open: 'Açık',
    InProgress: 'Devam ediyor',
    Closed: 'Kapandı',
};

/** A team member (userId set) or someone outside the system (userId null). */
export interface Owner {
    userId: number | null;
    name: string;
}

export interface SourceMeeting {
    id: number;
    code: string;
}

/** A row of GET /api/v1/projects/{projectId}/open-issues. Dates without time come as "2026-11-05". */
export interface OpenIssueListItem {
    id: number;
    code: string;
    type: OpenIssueType;
    title: string;
    owner: Owner;
    dueDate: string | null;
    status: OpenIssueStatus;
    isOverdue: boolean;
    sourceMeeting: SourceMeeting | null;
}

export interface OpenIssue extends OpenIssueListItem {
    projectId: number;
    description: string | null;
    closedAt: string | null;
    createdBy: UserSummary;
    createdAt: string;
    updatedAt: string | null;
}

export interface OpenIssueFilter {
    status?: OpenIssueStatus;
    type?: OpenIssueType;
    overdueOnly?: boolean;
}

/** POST and PUT body. Send exactly one of ownerUserId and ownerName. */
export interface SaveOpenIssue {
    type: OpenIssueType;
    title: string;
    description: string | null;
    ownerUserId: number | null;
    ownerName: string | null;
    dueDate: string | null;
}
