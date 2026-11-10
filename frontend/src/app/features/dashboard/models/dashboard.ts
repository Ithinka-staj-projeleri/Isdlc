import { UserSummary } from '../../../shared/models/user-summary';
import { OpenIssueStatus, OpenIssueType, Owner } from '../../open-issues/models/open-issue';
import { StepAction } from '../../process/models/project-process';

// Shapes of the Dashboard API. Contract: docs/dashboard-api.md

/** All: every project; Mine: only projects where the user is the analyst or the project manager. */
export type DashboardScope = 'All' | 'Mine';

export interface DashboardProject {
  id: number;
  code: string;
  name: string;
}

export interface FinishedThisYear {
  closed: number;
  lost: number;
  rejected: number;
  redirected: number;
}

export interface DashboardSummary {
  waitingRequests: number;
  activeProjects: number;
  myWaitingSteps: number;
  overdueOpenIssues: number;
  overdueProjects: number;
  finishedThisYear: FinishedThisYear;
}

export interface StageCount {
  stageOrder: number;
  shortTitle: string;
  count: number;
}

/** The first rows of a list (at most 10) and how many there are in total. */
export interface DashboardList<T> {
  totalCount: number;
  items: T[];
}

export interface WaitingStep {
  project: DashboardProject;
  step: { id: number; code: string; stageOrder: number; stageShortTitle: string };
  isApproval: boolean;
  isDecision: boolean;
  /** UTC date and time. */
  waitingSince: string;
  waitingDays: number;
}

export interface DashboardOpenIssue {
  project: DashboardProject;
  id: number;
  code: string;
  type: OpenIssueType;
  title: string;
  owner: Owner;
  /** Date only, e.g. "2026-11-12". */
  dueDate: string | null;
  status: OpenIssueStatus;
  isOverdue: boolean;
}

export interface RecentActivity {
  project: DashboardProject;
  stepCode: string;
  action: StepAction;
  performedBy: UserSummary;
  /** UTC date and time. */
  performedAt: string;
}

/** GET /api/v1/dashboard */
export interface Dashboard {
  scope: DashboardScope;
  summary: DashboardSummary;
  stages: StageCount[];
  myWaitingSteps: DashboardList<WaitingStep>;
  myOpenIssues: DashboardList<DashboardOpenIssue>;
  overdueOpenIssues: DashboardList<DashboardOpenIssue>;
  recentActivity: RecentActivity[];
}
