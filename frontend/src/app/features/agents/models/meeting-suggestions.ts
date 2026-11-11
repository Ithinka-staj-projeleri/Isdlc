import { MeetingType } from '../../meetings/models/meeting';
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
    /** Type, participants and agenda; null when the agent could not draft them. */
    minutes: MinutesDraft | null;
}

/** The form fills only the fields the person has not filled yet. */
export interface MinutesDraft {
    type: MeetingType;
    /** Why the model chose this type. */
    typeReason: string | null;
    /** Comma separated, as the form takes it. */
    participants: string;
    /** One "- " line per topic. */
    agenda: string;
    warnings: string[];
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
