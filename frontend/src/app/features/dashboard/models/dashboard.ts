import { UserSummary } from '../../../shared/models/user-summary';
import { CustomerFitRating } from '../../assessment/models/assessment';
import { OpenIssueStatus, OpenIssueType, Owner } from '../../open-issues/models/open-issue';
import { StepAction } from '../../process/models/project-process';
import { Permission } from '../../../shared/models/permission';
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

/** An active external request at the customer fit step (4) and its survey; rating stays null until every question is answered. */
export interface AcceptanceQueueItem {
  project: DashboardProject;
  stepCode: string;
  /** 0–100. */
  score: number;
  rating: CustomerFitRating | null;
  answeredCount: number;
  questionCount: number;
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
  /** Faz 5 · F2: mine, not closed, due from today to Sunday. */
  myOpenIssuesDueThisWeek: DashboardList<DashboardOpenIssue>;
  overdueOpenIssues: DashboardList<DashboardOpenIssue>;
  recentActivity: RecentActivity[];
  highRisks: DashboardList<HighRisk>;
  levels: LevelCounts;
  acceptanceQueue: DashboardList<AcceptanceQueueItem>;
  /** Faz 5 · F2: every active user, the busiest first; the same whatever the scope. */
  teamWorkload: TeamMemberWorkload[];
}

/** One team member's open issues and waiting steps on active records (shared with the person profile, F9). */
export interface TeamMemberWorkload {
  user: UserSummary;
  permissions: Permission[];
  openIssues: number;
  overdueOpenIssues: number;
  waitingSteps: number;
}
