import { improvementLink, projectLink, projectRecordLink, recordIdOf, recordLinkFrom } from './record-link';

describe('record links', () => {
  it('builds the project, improvement and record links', () => {
    expect(projectLink(14)).toEqual({ commands: ['/projects', 14] });
    expect(improvementLink(30)).toEqual({ commands: ['/process-improvements', 30] });
    expect(projectRecordLink(14, 'risks', 12)).toEqual({ commands: ['/projects', 14], queryParams: { tab: 'risks', id: '12' } });
  });
});

describe('recordIdOf', () => {
  it('reads only a positive whole number', () => {
    expect(recordIdOf('12')).toBe(12);
    expect(recordIdOf(null)).toBeNull();
    expect(recordIdOf('')).toBeNull();
    expect(recordIdOf('0')).toBeNull();
    expect(recordIdOf('-3')).toBeNull();
    expect(recordIdOf('1.5')).toBeNull();
    expect(recordIdOf('abc')).toBeNull();
  });
});

describe('recordLinkFrom', () => {
  it('keeps the project or improvement page the record is open on', () => {
    expect(recordLinkFrom('/projects/14?tab=risks', 14, 'risks', 12))
      .toEqual({ commands: ['/projects', 14], queryParams: { tab: 'risks', id: '12' } });
    expect(recordLinkFrom('/process-improvements/30?tab=meetings&id=4', 30, 'meetings', 5))
      .toEqual({ commands: ['/process-improvements', 30], queryParams: { tab: 'meetings', id: '5' } });
  });

  it("uses the project's page on a menu page or another project's page", () => {
    const expected = { commands: ['/projects', 14], queryParams: { tab: 'open-issues', id: '7' } };
    expect(recordLinkFrom('/open-issues', 14, 'open-issues', 7)).toEqual(expected);
    expect(recordLinkFrom('/projects/140', 14, 'open-issues', 7)).toEqual(expected);
    expect(recordLinkFrom('/projects/14/edit', 14, 'open-issues', 7))
      .toEqual({ commands: ['/projects', 14], queryParams: { tab: 'open-issues', id: '7' } });
  });
});

// ---- Cross tests (PLANNING.md): Erva → Baran's modules. Contract: docs/record-links.md ----

describe('record links (cross tests)', () => {
  it('uses the project detail tab names of L1 for every record kind', () => {
    for (const tab of ['open-issues', 'risks', 'requirements', 'meetings'] as const) {
      expect(projectRecordLink(14, tab, 3).queryParams).toEqual({ tab, id: '3' });
    }
  });

  it('puts no query in a project or improvement link, so it opens on its first tab', () => {
    expect(projectLink(14).queryParams).toBeUndefined();
    expect(improvementLink(30).queryParams).toBeUndefined();
  });

  it('reads an id with leading zeros as the same record and refuses anything that is not only digits', () => {
    expect(recordIdOf('007')).toBe(7);
    expect(recordIdOf(' 12')).toBeNull();
    expect(recordIdOf('12 ')).toBeNull();
    expect(recordIdOf('12abc')).toBeNull();
    expect(recordIdOf('1e3')).toBeNull();
  });

  it('keeps the page when its address ends with a slash or a fragment', () => {
    const expected = { commands: ['/projects', 14], queryParams: { tab: 'risks', id: '12' } };
    expect(recordLinkFrom('/projects/14/', 14, 'risks', 12)).toEqual(expected);
    expect(recordLinkFrom('/projects/14#ozet', 14, 'risks', 12)).toEqual(expected);
  });

  it("uses the record's project when another improvement is open", () => {
    expect(recordLinkFrom('/process-improvements/31?tab=meetings', 30, 'meetings', 5))
      .toEqual({ commands: ['/projects', 30], queryParams: { tab: 'meetings', id: '5' } });
  });

  it('does not take a page whose number only starts with the project id', () => {
    expect(recordLinkFrom('/process-improvements/300', 30, 'meetings', 5))
      .toEqual({ commands: ['/projects', 30], queryParams: { tab: 'meetings', id: '5' } });
  });
});
