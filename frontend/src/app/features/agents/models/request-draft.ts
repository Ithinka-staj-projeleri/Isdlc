import { RequestType } from '../../projects/project.model';

/**
 * POST /api/v1/agents/request-draft — the new request form filled from an e-mail; nothing is saved.
 * A field is null when the e-mail does not say it. The form sets the channel to E-posta itself.
 */
export interface RequestDraft {
    name: string | null;
    description: string | null;
    customerName: string | null;
    requesterName: string | null;
    requesterContact: string | null;
    requestType: RequestType | null;
    /** "yyyy-MM-dd" */
    requestDate: string | null;
    dueDate: string | null;
    notes: string | null;
    /** What to check before saving: guesses, values the form could not take. */
    warnings: string[];
    modelName: string;
}
