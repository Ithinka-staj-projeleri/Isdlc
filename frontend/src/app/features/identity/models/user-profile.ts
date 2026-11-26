import { Permission } from '../../../shared/models/permission';
import { DashboardList, DashboardOpenIssue, WaitingStep } from '../../dashboard/models/dashboard';

/** The person's numbers from the team workload (the same as the dashboard's "Ekip iş yükü" row). */
export interface UserWorkloadCounts {
  openIssues: number;
  overdueOpenIssues: number;
  waitingSteps: number;
}

/** GET /api/v1/users/{id}/profile (Faz 5 · F9). Contract: docs/users-api.md */
export interface UserProfile {
  id: number;
  fullName: string;
  email: string;
  permissions: Permission[];
  isActive: boolean;
  /** Comma separated areas, e.g. "Ödeme sistemleri, SQL". */
  expertise: string | null;
  workingNote: string | null;
  /** null for an inactive user. */
  workload: UserWorkloadCounts | null;
  openIssues: DashboardList<DashboardOpenIssue>;
  waitingSteps: DashboardList<WaitingStep>;
}

/** PUT /api/v1/users/me/profile */
export interface UpdateMyProfile {
  expertise: string | null;
  workingNote: string | null;
}

/** GET /api/v1/users/workload: an active user to pick as an owner, sorted by name. */
export interface UserWorkload extends UserWorkloadCounts {
  id: number;
  fullName: string;
  permissions: Permission[];
  expertise: string | null;
}

/** Field lengths (docs/users-api.md, P1). */
export const EXPERTISE_MAX_LENGTH = 300;
export const WORKING_NOTE_MAX_LENGTH = 1000;

/** The expertise text as separate areas: split on commas, trimmed, empty parts left out. */
export function expertiseAreas(expertise: string | null | undefined): string[] {
  return (expertise ?? '')
    .split(',')
    .map(area => area.trim())
    .filter(area => area.length > 0);
}

/** The owner selector's option text: "Elif Yılmaz · 3 açık konu (1 gecikmiş)". */
export function ownerOptionLabel(user: UserWorkload): string {
  const late = user.overdueOpenIssues > 0 ? ` (${user.overdueOpenIssues} gecikmiş)` : '';
  return `${user.fullName} · ${user.openIssues} açık konu${late}`;
}
