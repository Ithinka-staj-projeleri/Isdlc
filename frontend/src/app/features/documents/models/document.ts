import { BadgeTone } from '../../../shared/components/status-badge/status-badge.component';
import { UserSummary } from '../../../shared/models/user-summary';

// Shapes of the Documents API. Contract: docs/requirements-documents-api.md

export type DocumentStatus = 'NotStarted' | 'InProgress' | 'InReview' | 'Approved' | 'NotRequired';

export const DOCUMENT_STATUS_LABELS: Record<DocumentStatus, string> = {
  NotStarted: 'Başlanmadı',
  InProgress: 'Hazırlanıyor',
  InReview: 'İncelemede',
  Approved: 'Onaylandı',
  NotRequired: 'Gerekmiyor',
};

export const DOCUMENT_STATUS_TONES: Record<DocumentStatus, BadgeTone> = {
  NotStarted: 'neutral',
  InProgress: 'warn',
  InReview: 'info',
  Approved: 'ok',
  NotRequired: 'neutral',
};

/** A tracked document, as shown inside a project's document row. */
export interface DocumentTypeSummary {
  id: number;
  code: string;
  name: string;
  stageOrder: number;
  stageShortTitle: string;
}

/** One row of the Document Library (GET /api/v1/document-types). */
export interface DocumentType extends DocumentTypeSummary {
  description: string | null;
}

/**
 * A document in a project (GET /api/v1/projects/{projectId}/documents).
 * Never saved on the project: NotStarted with no note, link or user.
 */
export interface ProjectDocument {
  documentType: DocumentTypeSummary;
  status: DocumentStatus;
  note: string | null;
  link: string | null;
  /** Who saved it last. */
  updatedBy: UserSummary | null;
  /** UTC date and time, e.g. "2026-11-09T10:30:00Z". */
  updatedAt: string | null;
}

/** PUT body. */
export interface SaveProjectDocument {
  status: DocumentStatus;
  note: string | null;
  link: string | null;
}
