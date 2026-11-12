import { Answer, Question, QuestionOption, QuestionnaireItem } from './question-bank.models';
import { countOf, degreeOf, groupQuestions, isMissingNote, withLevel, withOption, withText } from './questionnaire-view';

// The risk analysis form's shared options.
const riskPresent: QuestionOption = { id: 1, code: 'risk-present', label: 'Risk Var', points: null, meaning: 'Negative', isScored: true, requiresNote: true };
const uncertain: QuestionOption = { id: 2, code: 'uncertain', label: 'Belirsiz', points: null, meaning: 'Open', isScored: true, requiresNote: false };
const noRisk: QuestionOption = { id: 3, code: 'no-risk', label: 'Risk Yok', points: null, meaning: 'Complete', isScored: false, requiresNote: false };

function question(id: number, section: string | null = null, group: string | null = null): Question {
  return {
    id, code: `risk-analysis/new-software-project/all/${id}`, no: id, section, group, topic: null, text: `Soru ${id}?`,
    requirement: 'Required', expectedInfo: null, riskDescription: null, suggestedAction: null, reference: null,
    options: [riskPresent, uncertain, noRisk],
  };
}

function answer(change: Partial<Answer>): Answer {
  return { optionId: null, text: null, note: null, probability: null, severity: null, updatedBy: null, updatedAt: null, ...change };
}

function item(q: Question, a: Partial<Answer> | null = null): QuestionnaireItem {
  return { question: q, answer: a ? answer(a) : null };
}

describe('groupQuestions', () => {
  it('starts a new heading whenever the section or group changes, keeping the order', () => {
    const items = [
      item(question(1, '1-Temel', 'İş İhtiyacı')),
      item(question(2, '1-Temel', 'İş İhtiyacı')),
      item(question(3, '1-Temel', 'Paydaşlar')),
      item(question(4, '2-Kapsam', 'Paydaşlar')),
    ];

    const sections = groupQuestions(items);

    expect(sections.map(s => s.section)).toEqual(['1-Temel', '2-Kapsam']);
    expect(sections[0].groups.map(g => [g.group, g.items.map(i => i.question.no)])).toEqual([
      ['İş İhtiyacı', [1, 2]],
      ['Paydaşlar', [3]],
    ]);
    expect(sections[1].groups[0].items.map(i => i.question.no)).toEqual([4]);
  });

  it('puts a form without sections or groups under one heading', () => {
    const sections = groupQuestions([item(question(1)), item(question(2))]);

    expect(sections).toHaveLength(1);
    expect(sections[0].groups).toHaveLength(1);
    expect(sections[0].groups[0].items).toHaveLength(2);
  });
});

describe('withOption', () => {
  it('chooses the option and keeps text and note', () => {
    const next = withOption(item(question(1), { note: 'Not', text: 'Yanıt' }), noRisk.id);

    expect(next).toEqual({ optionId: noRisk.id, text: 'Yanıt', note: 'Not', probability: null, severity: null });
  });

  it('clears the option when the chosen one is clicked again', () => {
    expect(withOption(item(question(1), { optionId: noRisk.id }), noRisk.id).optionId).toBeNull();
  });

  it('keeps probability and severity between scored options, drops them for an unscored one', () => {
    const scored = item(question(1), { optionId: riskPresent.id, probability: 2, severity: 3 });

    expect(withOption(scored, uncertain.id)).toMatchObject({ probability: 2, severity: 3 });
    expect(withOption(scored, noRisk.id)).toMatchObject({ probability: null, severity: null });
  });
});

describe('withText and withLevel', () => {
  it('trims the text and sends an empty one as null', () => {
    expect(withText(item(question(1)), 'note', '  ERP gecikebilir. ').note).toBe('ERP gecikebilir.');
    expect(withText(item(question(1), { note: 'x' }), 'note', '   ').note).toBeNull();
  });

  it('sets a level and clears it on a second click', () => {
    const scored = item(question(1), { optionId: riskPresent.id, probability: 2 });

    expect(withLevel(scored, 'severity', 3)).toMatchObject({ probability: 2, severity: 3 });
    expect(withLevel(scored, 'probability', 2).probability).toBeNull();
  });
});

describe('degreeOf, isMissingNote and countOf', () => {
  it('multiplies probability and severity only when both are given', () => {
    expect(degreeOf(answer({ probability: 2, severity: 3 }))).toBe(6);
    expect(degreeOf(answer({ probability: 2 }))).toBeNull();
    expect(degreeOf(null)).toBeNull();
  });

  it('warns when the chosen option asks for a note and none is written', () => {
    expect(isMissingNote(item(question(1), { optionId: riskPresent.id }))).toBe(true);
    expect(isMissingNote(item(question(1), { optionId: riskPresent.id, note: 'Neden' }))).toBe(false);
    expect(isMissingNote(item(question(1), { optionId: noRisk.id }))).toBe(false);
  });

  it('reads the summary count of a meaning', () => {
    const summary = {
      listed: 3, answered: 2, requiredUnanswered: 1, isComplete: false, missingNote: 0,
      byMeaning: { complete: 1, partial: 0, open: 0, negative: 1, notApplicable: 0 },
    };

    expect(countOf(summary, 'Negative')).toBe(1);
    expect(countOf(summary, 'NotApplicable')).toBe(0);
  });
});
