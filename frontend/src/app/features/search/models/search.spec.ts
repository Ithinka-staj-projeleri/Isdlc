import { SearchGroup, SearchRecord, SearchResult, searchText, toRowGroups } from './search';

function empty<T>(): SearchGroup<T> {
  return { items: [], totalCount: 0 };
}

function result(change: Partial<SearchResult>): SearchResult {
  return {
    projects: empty(), improvements: empty(), openIssues: empty(), meetings: empty(), risks: empty(), requirements: empty(),
    ...change,
  };
}

function record(id: number, status: string): SearchRecord {
  return { id, code: `RSK-00${id}`, title: `Kayıt ${id}`, status, projectId: 14, projectCode: '14', projectName: 'Bayi Portalı' };
}

describe('searchText', () => {
  it('trims the text and sends nothing under 2 characters', () => {
    expect(searchText('  a ')).toBeNull();
    expect(searchText('   ')).toBeNull();
    expect(searchText(' portal ')).toBe('portal');
  });

  it('cuts the text at 100 characters', () => {
    expect(searchText('x'.repeat(150))?.length).toBe(100);
  });
});

describe('toRowGroups', () => {
  it('leaves out empty groups and keeps the panel order', () => {
    const groups = toRowGroups(result({
      requirements: { items: [record(1, 'Draft')], totalCount: 1 },
      projects: { items: [{ id: 14, code: '14', name: 'Bayi Portalı', customerName: 'Demo A.Ş.', status: 'Active' }], totalCount: 1 },
    }));

    expect(groups.map(g => g.label)).toEqual(['Projeler', 'Gereksinimler']);
  });

  it('links a project to its page and an improvement to the improvement page', () => {
    const groups = toRowGroups(result({
      projects: { items: [{ id: 14, code: '14', name: 'Bayi Portalı', customerName: 'Demo A.Ş.', status: 'Active' }], totalCount: 1 },
      improvements: { items: [{ id: 30, code: 'SI-001', name: 'Notlar', status: 'Active' }], totalCount: 1 },
    }));

    expect(groups[0].rows[0].link).toEqual({ commands: ['/projects', 14] });
    expect(groups[0].rows[0].subtitle).toBe('Demo A.Ş.');
    expect(groups[1].rows[0].link).toEqual({ commands: ['/process-improvements', 30] });
  });

  it('links a record inside a project to the project on its tab and names the project', () => {
    const groups = toRowGroups(result({
      openIssues: { items: [{ ...record(7, 'Open'), code: 'AK-007' }], totalCount: 1 },
      meetings: { items: [{ id: 4, code: 'TT-002', title: 'Kapsam', date: '2026-11-12', projectId: 14, projectCode: '14', projectName: 'Bayi Portalı' }], totalCount: 1 },
      risks: { items: [record(1, 'Open')], totalCount: 1 },
      requirements: { items: [record(2, 'Draft')], totalCount: 1 },
    }));

    expect(groups.map(g => g.rows[0].link.queryParams?.['tab'])).toEqual(['open-issues', 'meetings', 'risks', 'requirements']);
    expect(groups.every(g => g.rows[0].link.commands.join('/') === '/projects/14')).toBe(true);
    expect(groups[0].rows[0].subtitle).toBe('14 · Bayi Portalı');
  });

  it('marks only closed records, with their status', () => {
    const groups = toRowGroups(result({
      projects: {
        items: [
          { id: 1, code: '1', name: 'Açık', customerName: '', status: 'Active' },
          { id: 2, code: '2', name: 'Ertelenen', customerName: '', status: 'Postponed' },
          { id: 3, code: '3', name: 'Kapanan', customerName: '', status: 'Closed' },
        ],
        totalCount: 3,
      },
      risks: { items: [record(1, 'Monitoring'), record(2, 'Closed')], totalCount: 2 },
      requirements: { items: [record(3, 'Approved'), record(4, 'Cancelled')], totalCount: 2 },
    }));

    expect(groups.map(g => g.rows.map(r => r.closedLabel !== null))).toEqual([
      [false, false, true],
      [false, true],
      [false, true],
    ]);
    expect(groups[0].rows[0].subtitle).toBeNull();
  });

  it('keeps every row key unique across groups', () => {
    const groups = toRowGroups(result({
      openIssues: { items: [record(1, 'Open')], totalCount: 1 },
      risks: { items: [record(1, 'Open')], totalCount: 1 },
    }));

    expect(new Set(groups.flatMap(g => g.rows.map(r => r.key))).size).toBe(2);
  });
});
