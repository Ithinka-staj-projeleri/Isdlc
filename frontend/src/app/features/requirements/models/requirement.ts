import { BadgeTone } from '../../../shared/components/status-badge/status-badge.component';
import { UserSummary } from '../../../shared/models/user-summary';

// Shapes of the Requirements API. Contract: docs/requirements-documents-api.md

export type RequirementType = 'Business' | 'Functional' | 'NonFunctional';
export type RequirementPriority = 'MustHave' | 'ShouldHave' | 'CouldHave' | 'WontHave';
export type RequirementStatus = 'Draft' | 'InReview' | 'Approved' | 'Cancelled';

export const REQUIREMENT_TYPE_LABELS: Record<RequirementType, string> = {
  Business: 'İş gereksinimi',
  Functional: 'Fonksiyonel',
  NonFunctional: 'Fonksiyonel olmayan',
};

/** MoSCoW. */
export const REQUIREMENT_PRIORITY_LABELS: Record<RequirementPriority, string> = {
  MustHave: 'Olmazsa olmaz',
  ShouldHave: 'Olmalı',
  CouldHave: 'Olabilir',
  WontHave: 'Bu sürümde yok',
};

export const REQUIREMENT_STATUS_LABELS: Record<RequirementStatus, string> = {
  Draft: 'Taslak',
  InReview: 'İncelemede',
  Approved: 'Onaylandı',
  Cancelled: 'İptal edildi',
};

export const REQUIREMENT_PRIORITY_TONES: Record<RequirementPriority, BadgeTone> = {
  MustHave: 'warn',
  ShouldHave: 'info',
  CouldHave: 'neutral',
  WontHave: 'neutral',
};

export const REQUIREMENT_STATUS_TONES: Record<RequirementStatus, BadgeTone> = {
  Draft: 'neutral',
  InReview: 'info',
  Approved: 'ok',
  Cancelled: 'bad',
};

/** A row of GET /api/v1/projects/{projectId}/requirements. */
export interface RequirementListItem {
  id: number;
  code: string;
  type: RequirementType;
  title: string;
  priority: RequirementPriority;
  status: RequirementStatus;
}

export interface Requirement extends RequirementListItem {
  projectId: number;
  description: string | null;
  acceptanceCriteria: string | null;
  createdBy: UserSummary;
  /** UTC date and time, e.g. "2026-11-09T09:00:00Z". */
  createdAt: string;
  updatedAt: string | null;
}

/** List filters; all optional. */
export interface RequirementFilter {
  status?: RequirementStatus;
  type?: RequirementType;
  priority?: RequirementPriority;
  search?: string;
}

/** POST and PUT body. A new record always starts as Draft; the status is changed separately. */
export interface SaveRequirement {
  type: RequirementType;
  title: string;
  description: string | null;
  acceptanceCriteria: string | null;
  priority: RequirementPriority;
}
