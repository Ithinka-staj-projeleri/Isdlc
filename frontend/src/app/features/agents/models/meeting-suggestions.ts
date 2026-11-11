import { OpenIssueType } from '../../open-issues/models/open-issue';

/** GET /api/v1/agents/status */
export interface AgentStatus {
    meetingAssistantEnabled: boolean;
    riskAssistantEnabled: boolean;
}

/** POST /api/v1/projects/{projectId}/meetings/suggestions — nothing is saved; the form is filled for review. */
export interface MeetingSuggestions {
    summary: string;
    decisions: SuggestedDecision[];
    actions: SuggestedAction[];
    /** Questions, dependencies...: no place in the minutes form, shown for manual entry on Open Issues. */
    others: SuggestedOpenIssue[];
    /** Items the model returned that did not pass validation. */
    skippedCount: number;
    modelName: string;
}

export interface SuggestedDecision {
    title: string;
    /** The sentence in the notes the suggestion is based on. */
    evidence: string;
}

/** ownerUserId is set when the name in the notes matches an active user; otherwise ownerName (or nobody). */
export interface SuggestedAction {
    title: string;
    ownerUserId: number | null;
    ownerName: string | null;
    dueDate: string | null;
    evidence: string;
    warnings: string[];
}

export interface SuggestedOpenIssue {
    type: OpenIssueType;
    title: string;
    ownerName: string | null;
    dueDate: string | null;
    evidence: string;
}
