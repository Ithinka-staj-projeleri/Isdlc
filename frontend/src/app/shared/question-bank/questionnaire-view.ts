import {
  Answer, OptionMeaning, QuestionOption, QuestionnaireItem, Questionnaire, QuestionnaireSummary, SaveAnswer, SaveQuestionnaire,
} from './question-bank.models';

/** A section of the form ("1-Temel") with its groups, in the form's order. */
export interface SectionView {
  section: string | null;
  groups: GroupView[];
}

export interface GroupView {
  group: string | null;
  items: QuestionnaireItem[];
}

/**
 * Splits the listed questions into sections and groups without changing their order: a new heading starts
 * whenever the section or group changes, like the rows of the Excel form.
 */
export function groupQuestions(items: QuestionnaireItem[]): SectionView[] {
  const sections: SectionView[] = [];
  for (const item of items) {
    let section = sections.at(-1);
    if (!section || section.section !== item.question.section) {
      section = { section: item.question.section, groups: [] };
      sections.push(section);
    }

    let group = section.groups.at(-1);
    if (!group || group.group !== item.question.group) {
      group = { group: item.question.group, items: [] };
      section.groups.push(group);
    }

    group.items.push(item);
  }

  return sections;
}

/** The chosen option of an answer, if any. */
export function chosenOption(item: QuestionnaireItem): QuestionOption | null {
  const id = item.answer?.optionId;
  return id == null ? null : item.question.options.find(o => o.id === id) ?? null;
}

/** The answer as it is sent back: the saved one with the change applied. */
function payloadOf(answer: Answer | null): SaveAnswer {
  return {
    optionId: answer?.optionId ?? null,
    text: answer?.text ?? null,
    note: answer?.note ?? null,
    probability: answer?.probability ?? null,
    severity: answer?.severity ?? null,
  };
}

/**
 * The answer after an option click. Clicking the chosen option again clears it. Probability and severity are
 * kept only while the new option is scored (the API refuses them otherwise); text and note always stay.
 */
export function withOption(item: QuestionnaireItem, optionId: number): SaveAnswer {
  const current = payloadOf(item.answer);
  const option = current.optionId === optionId ? null : item.question.options.find(o => o.id === optionId) ?? null;

  return {
    ...current,
    optionId: option?.id ?? null,
    probability: option?.isScored ? current.probability : null,
    severity: option?.isScored ? current.severity : null,
  };
}

/** The answer after the note or the stakeholder's text was edited; empty text is sent as null. */
export function withText(item: QuestionnaireItem, field: 'text' | 'note', value: string): SaveAnswer {
  const trimmed = value.trim();
  return { ...payloadOf(item.answer), [field]: trimmed === '' ? null : trimmed };
}

/** The answer after a probability or severity click (1–3); clicking the chosen value again clears it. */
export function withLevel(item: QuestionnaireItem, field: 'probability' | 'severity', value: number): SaveAnswer {
  const current = payloadOf(item.answer);
  return { ...current, [field]: current[field] === value ? null : value };
}

/** Risk degree on the 3 × 3 scale: probability × severity (1–9); null until both are given. */
export function degreeOf(answer: Answer | null): number | null {
  return answer?.probability && answer.severity ? answer.probability * answer.severity : null;
}

/** True when the chosen option asks for a note and none was written. */
export function isMissingNote(item: QuestionnaireItem): boolean {
  return !!chosenOption(item)?.requiresNote && !item.answer?.note?.trim();
}

/** How many answers have the meaning ("Complete" → summary.byMeaning.complete). */
export function countOf(summary: QuestionnaireSummary, meaning: OptionMeaning): number {
  const key = (meaning.charAt(0).toLowerCase() + meaning.slice(1)) as keyof QuestionnaireSummary['byMeaning'];
  return summary.byMeaning[key];
}

/** The form's header as it is sent back, with one field changed. */
export function headerWith(form: Questionnaire, change: Partial<SaveQuestionnaire>): SaveQuestionnaire {
  return {
    subcategory: form.subcategory,
    participants: form.participants,
    assessedOn: form.assessedOn,
    approvedByUserId: form.approvedBy?.id ?? null,
    approvedOn: form.approvedOn,
    ...change,
  };
}
