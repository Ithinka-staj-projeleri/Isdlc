import { UserWorkload, expertiseAreas, ownerOptionLabel } from './user-profile';

describe('expertiseAreas', () => {
  it('splits on commas and trims each area', () => {
    expect(expertiseAreas(' Ödeme sistemleri,SQL ,  Raporlama ')).toEqual(['Ödeme sistemleri', 'SQL', 'Raporlama']);
  });

  it('leaves out empty parts', () => {
    expect(expertiseAreas('SQL,, ,')).toEqual(['SQL']);
  });

  it('returns nothing for no expertise', () => {
    expect(expertiseAreas(null)).toEqual([]);
    expect(expertiseAreas('')).toEqual([]);
  });
});

describe('ownerOptionLabel', () => {
  const user: UserWorkload = {
    id: 1, fullName: 'Elif Yılmaz', role: 'BusinessAnalyst', expertise: null,
    openIssues: 3, overdueOpenIssues: 0, waitingSteps: 2,
  };

  it('shows the open issue count', () => {
    expect(ownerOptionLabel(user)).toBe('Elif Yılmaz · 3 açık konu');
  });

  it('adds the overdue count when there is one', () => {
    expect(ownerOptionLabel({ ...user, overdueOpenIssues: 1 })).toBe('Elif Yılmaz · 3 açık konu (1 gecikmiş)');
  });
});
