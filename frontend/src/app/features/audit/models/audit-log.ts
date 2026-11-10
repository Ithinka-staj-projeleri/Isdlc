import { UserSummary } from '../../../shared/models/user-summary';

/** Entity names written by the backend's AuditLogInterceptor. */
export type AuditEntityType = 'Project' | 'OpenIssue' | 'Meeting' | 'Requirement' | 'ProjectDocument' | 'Risk' | 'ProjectAssessment';
export type AuditAction = 'Created' | 'Updated' | 'Deleted';

/** A row of GET /api/v1/projects/{projectId}/audit-logs (newest first). */
export interface AuditLog {
    id: number;
    /** A C# entity name; usually one of AuditEntityType, but older rows may hold others. */
    entityType: string;
    entityId: number;
    /** "AK-003", "TT-002", the project code or the document's name; null when the record is gone. */
    recordCode: string | null;
    action: AuditAction;
    changes: AuditFieldChange[];
    performedBy: UserSummary;
    performedAt: string;
}

/**
 * One changed field. Values are text: enum names ("Closed"), dates ("2026-11-12"), "true"/"false",
 * and references already readable (user names, step and meeting codes). null: empty / no old value.
 */
export interface AuditFieldChange {
    field: string;
    oldValue: string | null;
    newValue: string | null;
}
