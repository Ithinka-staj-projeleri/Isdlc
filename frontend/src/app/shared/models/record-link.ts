/** Links that open a record (Faz 5 · F8). Contract: docs/record-links.md */

/** Router commands and query params; the same shape for routerLink, router.navigate and the copy button. */
export interface RecordLink {
  commands: (string | number)[];
  queryParams?: Record<string, string>;
}

/** The project detail tabs that hold records with their own form or detail. */
export type RecordTab = 'open-issues' | 'risks' | 'requirements' | 'meetings';

export function projectLink(projectId: number): RecordLink {
  return { commands: ['/projects', projectId] };
}

export function improvementLink(projectId: number): RecordLink {
  return { commands: ['/process-improvements', projectId] };
}

/** /projects/14?tab=risks&id=12: the project opens on the tab, and the tab opens the record. */
export function projectRecordLink(projectId: number, tab: RecordTab, recordId: number): RecordLink {
  return { commands: ['/projects', projectId], queryParams: { tab, id: String(recordId) } };
}

/** The record id in the address (?id=12); null when missing or not a positive whole number. */
export function recordIdOf(value: string | null): number | null {
  if (value === null || !/^\d+$/.test(value)) return null;
  const id = Number(value);
  return id > 0 ? id : null;
}

/**
 * A record's link from the page it is open on: the project or improvement page the user is on (both have record
 * tabs), or the project's page when the record is open on a menu page such as /risks.
 */
export function recordLinkFrom(currentUrl: string, projectId: number, tab: RecordTab, recordId: number): RecordLink {
  const page = /^\/(projects|process-improvements)\/(\d+)(?=[/?#]|$)/.exec(currentUrl);
  if (page && Number(page[2]) === projectId) {
    return { commands: ['/' + page[1], projectId], queryParams: { tab, id: String(recordId) } };
  }
  return projectRecordLink(projectId, tab, recordId);
}
