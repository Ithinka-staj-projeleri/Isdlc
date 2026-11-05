import { UserSummary } from '../../shared/models/user-summary';

// Shapes of the Projects API. Contract: docs/projects-api.md
// Enum values are the backend's text values; Turkish labels are in project-labels.ts.

export type ProjectStatus = 'Active' | 'Closed' | 'Rejected' | 'Lost' | 'Redirected';
export type RequestSource = 'Internal' | 'External';
export type RequestChannel = 'Written' | 'Verbal';
export type RequestType = 'NewProject' | 'AdditionalModule' | 'CompanyProduct' | 'Support';
export type ProjectPriority = 'Low' | 'Medium' | 'High';

/** Request = stages 1–3 ("Talepler"), Project = stages 4–13 ("Projeler"). Active projects only. */
export type ProjectPhase = 'Request' | 'Project';

/** The step an active project waits on. */
export interface CurrentStepSummary {
  id: number;
  code: string;
  stageOrder: number;
  stageShortTitle: string;
}

/** One row of the project list (GET /api/v1/projects). */
export interface ProjectListItem {
  id: number;
  code: string;
  name: string;
  customerName: string;
  requestType: RequestType;
  status: ProjectStatus;
  currentStep: CurrentStepSummary | null;
  businessAnalyst: UserSummary | null;
  level: number | null;
  priority: ProjectPriority | null;
  /** Date only, e.g. "2026-11-05". */
  startDate: string;
  dueDate: string | null;
}

/** Project detail (GET /api/v1/projects/{id}). */
export interface Project extends ProjectListItem {
  description: string;
  source: RequestSource;
  requestChannel: RequestChannel;
  requesterName: string;
  requiresNda: boolean;
  projectManager: UserSummary | null;
  createdBy: UserSummary;
  /** UTC date and time, e.g. "2026-11-05T09:00:00Z". */
  createdAt: string;
  updatedAt: string | null;
}

/**
 * What the request form sends (POST /api/v1/projects, later also PUT).
 * Status, current step, priority and level are not here: Process and Assessment own them.
 */
export interface ProjectInput {
  name: string;
  description: string;
  source: RequestSource;
  requestChannel: RequestChannel;
  requestType: RequestType;
  customerName: string;
  requesterName: string;
  requiresNda: boolean;
  businessAnalystId: number | null;
  projectManagerId: number | null;
  /** Date only, e.g. "2026-11-05". */
  startDate: string;
  dueDate: string | null;
}

/** List filters; all optional. */
export interface ProjectFilter {
  status?: ProjectStatus;
  phase?: ProjectPhase;
  search?: string;
  businessAnalystId?: number;
  page?: number;
  pageSize?: number;
}
