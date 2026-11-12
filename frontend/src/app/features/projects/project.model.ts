import { ProjectStatus } from '../../shared/models/project-status';
import { UserSummary } from '../../shared/models/user-summary';

// Shapes of the Projects API. Contract: docs/projects-api.md
// Enum values are the backend's text values; Turkish labels are in project-labels.ts.

/** Shared with other modules (shared/models/project-status). */
export type { ProjectStatus } from '../../shared/models/project-status';
export type RequestSource = 'Internal' | 'External' | 'Affiliated';
export type RequestChannel = 'Email' | 'Phone' | 'Meeting' | 'Other';
export type RequestType = 'NewProject' | 'AdditionalModule' | 'CompanyProduct' | 'Support';
export type ProjectPriority = 'Low' | 'Medium' | 'High';

/**
 * Every record is in exactly one phase.
 * Request = waiting request: active, before its flow's project stage ("Talepler").
 * Project = everything else: active projects from the project stage on and all closed records ("Projeler").
 */
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
  /** Proje No: a plain number as text, e.g. "14". */
  code: string;
  name: string;
  customerName: string;
  requestType: RequestType;
  status: ProjectStatus;
  currentStep: CurrentStepSummary | null;
  businessAnalyst: UserSummary | null;
  level: number | null;
  priority: ProjectPriority | null;
  /** Talep tarihi; date only, e.g. "2026-11-05". */
  requestDate: string;
  dueDate: string | null;
}

/** Project detail (GET /api/v1/projects/{id}). */
export interface Project extends ProjectListItem {
  description: string;
  source: RequestSource;
  requestChannel: RequestChannel;
  /** What "Diğer" means; only for the Other channel. */
  requestChannelOther: string | null;
  /** Talebi ileten kişi. */
  requesterName: string;
  requesterContact: string | null;
  notes: string | null;
  /** Whether an offer will be prepared; the offer steps of the flow are skipped when false. */
  requiresOffer: boolean;
  requiresNda: boolean;
  projectManager: UserSummary | null;
  /** Talebi kayda alan. */
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
  requestChannelOther: string | null;
  requestType: RequestType;
  customerName: string;
  requesterName: string;
  requesterContact: string | null;
  notes: string | null;
  requiresOffer: boolean;
  requiresNda: boolean;
  businessAnalystId: number | null;
  projectManagerId: number | null;
  /** Date only, e.g. "2026-11-05". */
  requestDate: string;
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
