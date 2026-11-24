import { OPEN_ISSUE_STATUS_LABELS, OpenIssueStatus } from '../../open-issues/models/open-issue';
import { REQUIREMENT_STATUS_LABELS, RequirementStatus } from '../../requirements/models/requirement';
import { RISK_STATUS_LABELS, RiskStatus } from '../../risks/models/risk';
import { PROJECT_STATUS_LABELS, ProjectStatus } from '../../../shared/models/project-status';
import {
  RecordLink, RecordTab, improvementLink, projectLink, projectRecordLink,
} from '../../../shared/models/record-link';

/** GET /api/v1/search. Contract: docs/search-api.md */

export const SEARCH_MIN_LENGTH = 2;
export const SEARCH_MAX_LENGTH = 100;

export interface SearchGroup<T> {
  items: T[];
  totalCount: number;
}

export interface SearchProject {
  id: number;
  code: string;
  name: string;
  customerName: string;
  status: ProjectStatus;
}

export interface SearchImprovement {
  id: number;
  code: string;
  name: string;
  status: ProjectStatus;
}

/** An open issue, a risk or a requirement; status is that record's own status. */
export interface SearchRecord {
  id: number;
  code: string;
  title: string;
  status: string;
  projectId: number;
  projectCode: string;
  projectName: string;
}

export interface SearchMeeting {
  id: number;
  code: string;
  title: string;
  date: string;
  projectId: number;
  projectCode: string;
  projectName: string;
}

export interface SearchResult {
  projects: SearchGroup<SearchProject>;
  improvements: SearchGroup<SearchImprovement>;
  openIssues: SearchGroup<SearchRecord>;
  meetings: SearchGroup<SearchMeeting>;
  risks: SearchGroup<SearchRecord>;
  requirements: SearchGroup<SearchRecord>;
}

/** One line in the results panel, ready to show and open. */
export interface SearchRow {
  /** Unique in the panel, for tracking and the keyboard selection. */
  key: string;
  code: string;
  title: string;
  /** "14 · Bayi Portalı" for a record inside a project; the customer for a project. */
  subtitle: string | null;
  /** Shown only for a closed record ("Kapandı", "İptal"). */
  closedLabel: string | null;
  /** The record's page (S9); a record inside a project opens itself on its tab (F8). */
  link: RecordLink;
}

export interface SearchRowGroup {
  label: string;
  totalCount: number;
  rows: SearchRow[];
}

/** The text that is sent: trimmed; null when it is too short to search. */
export function searchText(input: string): string | null {
  const text = input.trim().slice(0, SEARCH_MAX_LENGTH);
  return text.length >= SEARCH_MIN_LENGTH ? text : null;
}

const OPEN_PROJECT: ProjectStatus[] = ['Active', 'Postponed'];

function projectClosedLabel(status: ProjectStatus): string | null {
  return OPEN_PROJECT.includes(status) ? null : PROJECT_STATUS_LABELS[status];
}

function inProject(record: { projectCode: string; projectName: string }): string {
  return `${record.projectCode} · ${record.projectName}`;
}

/** A record inside a project opens its project on the record's tab, and the tab opens the record (F8). */
function recordRow(kind: string, tab: RecordTab, record: SearchRecord, closedLabel: string | null): SearchRow {
  return {
    key: `${kind}-${record.id}`,
    code: record.code,
    title: record.title,
    subtitle: inProject(record),
    closedLabel,
    link: projectRecordLink(record.projectId, tab, record.id),
  };
}

/** The groups in the order of the panel; empty groups are left out. */
export function toRowGroups(result: SearchResult): SearchRowGroup[] {
  const groups: SearchRowGroup[] = [
    {
      label: 'Projeler',
      totalCount: result.projects.totalCount,
      rows: result.projects.items.map(p => ({
        key: `project-${p.id}`,
        code: p.code,
        title: p.name,
        subtitle: p.customerName || null,
        closedLabel: projectClosedLabel(p.status),
        link: projectLink(p.id),
      })),
    },
    {
      label: 'Süreç iyileştirme',
      totalCount: result.improvements.totalCount,
      rows: result.improvements.items.map(p => ({
        key: `improvement-${p.id}`,
        code: p.code,
        title: p.name,
        subtitle: null,
        closedLabel: projectClosedLabel(p.status),
        link: improvementLink(p.id),
      })),
    },
    {
      label: 'Açık konular',
      totalCount: result.openIssues.totalCount,
      rows: result.openIssues.items.map(r => recordRow('open-issue', 'open-issues', r,
        r.status === 'Closed' ? OPEN_ISSUE_STATUS_LABELS[r.status as OpenIssueStatus] : null)),
    },
    {
      label: 'Tutanaklar',
      totalCount: result.meetings.totalCount,
      rows: result.meetings.items.map(m => ({
        key: `meeting-${m.id}`,
        code: m.code,
        title: m.title,
        subtitle: inProject(m),
        closedLabel: null,
        link: projectRecordLink(m.projectId, 'meetings', m.id),
      })),
    },
    {
      label: 'Riskler',
      totalCount: result.risks.totalCount,
      rows: result.risks.items.map(r => recordRow('risk', 'risks', r,
        r.status === 'Closed' ? RISK_STATUS_LABELS[r.status as RiskStatus] : null)),
    },
    {
      label: 'Gereksinimler',
      totalCount: result.requirements.totalCount,
      rows: result.requirements.items.map(r => recordRow('requirement', 'requirements', r,
        r.status === 'Cancelled' ? REQUIREMENT_STATUS_LABELS[r.status as RequirementStatus] : null)),
    },
  ];

  return groups.filter(g => g.rows.length > 0);
}
