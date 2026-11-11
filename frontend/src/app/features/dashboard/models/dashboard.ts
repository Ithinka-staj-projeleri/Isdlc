import { UserSummary } from '../../../shared/models/user-summary';
import { AcceptanceRecommendation } from '../../assessment/models/assessment';
import { OpenIssueStatus, OpenIssueType, Owner } from '../../open-issues/models/open-issue';
import { StepAction } from '../../process/models/project-process';
import { RiskStatus } from '../../risks/models/risk';

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
  /** Open high risks (score 15+) on active projects. */
  highRisks: number;
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

/** A risk that is not closed and whose score is high (15 or more). */
export interface HighRisk {
  project: DashboardProject;
  id: number;
  code: string;
  title: string;
  probability: number;
  impact: number;
  score: number;
  status: RiskStatus;
  owner: UserSummary;
}

/** Active records by applied level; notSet: no level applied yet (step 2.4). */
export interface LevelCounts {
  level1: number;
  level2: number;
  level3: number;
  notSet: number;
}

/** An active request at the customer suitability step (4); weightedScore and recommendation stay null until all six criteria are scored. */
export interface AcceptanceQueueItem {
  project: DashboardProject;
  stepCode: string;
  weightedScore: number | null;
  recommendation: AcceptanceRecommendation | null;
  answeredCount: number;
  /** UTC date and time. */
  waitingSince: string;
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
  highRisks: DashboardList<HighRisk>;
  levels: LevelCounts;
  acceptanceQueue: DashboardList<AcceptanceQueueItem>;
}
