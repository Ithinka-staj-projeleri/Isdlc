import { LevelResult, canApplyLevel, differsFromApplied } from './assessment';

function result(level: number | null, days: number | null, appliedLevel: number | null, appliedDays: number | null): LevelResult {
  return {
    documentLevel: { score: 0, maxScore: 60, answered: 0, listed: 12, level },
    research: { score: 0, maxScore: 60, answered: 0, listed: 12, days },
    appliedLevel,
    appliedResearchDays: appliedDays,
  };
}

describe('level result', () => {
  it('can be applied only when both sections have a result', () => {
    expect(canApplyLevel(result(2, 5, null, null))).toBe(true);
    expect(canApplyLevel(result(2, null, null, null))).toBe(false);
    expect(canApplyLevel(result(null, 5, null, null))).toBe(false);
  });

  it('differs from the project when the level or the days changed after applying', () => {
    expect(differsFromApplied(result(2, 5, 2, 5))).toBe(false);
    expect(differsFromApplied(result(3, 5, 2, 5))).toBe(true);
    expect(differsFromApplied(result(2, 10, 2, 5))).toBe(true);
    expect(differsFromApplied(result(2, 5, null, null))).toBe(true);
    // An incomplete form suggests nothing, so there is nothing to compare.
    expect(differsFromApplied(result(null, null, 2, 5))).toBe(false);
  });
});
