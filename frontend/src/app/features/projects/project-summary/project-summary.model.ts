import { OpenIssueType } from '../../open-issues/models/open-issue';

// GET /api/v1/projects/{projectId}/summary (Faz 5 · F1). Contract: docs/projects-api.md → "Proje özeti".

export interface ProjectSummary {
  openIssues: OpenIssueSummary;
  risks: RiskSummary;
  requirements: RequirementSummary;
  documents: DocumentSummary;
  /** Not-closed open issues per owner, busiest first. */
  workload: OwnerWorkload[];
}

export interface OpenIssueSummary {
  open: number;
  inProgress: number;
  closed: number;
  /** Not closed and past the due date. */
  overdue: number;
  closedLast30Days: number;
  /** Not closed, every type in order (zeros included). */
  byType: { type: OpenIssueType; count: number }[];
}

/** Risks still followed (open or monitored) by severity, and the closed ones. */
export interface RiskSummary {
  high: number;
  medium: number;
  low: number;
  closed: number;
}

export interface RequirementSummary {
  draft: number;
  inReview: number;
  approved: number;
  cancelled: number;
}

/** Document types followed on the project (not "Gerekli değil") and how many are approved. */
export interface DocumentSummary {
  approved: number;
  tracked: number;
}

/** A team member has a user id; someone outside the system has none. */
export interface OwnerWorkload {
  userId: number | null;
  name: string;
  open: number;
  overdue: number;
}
