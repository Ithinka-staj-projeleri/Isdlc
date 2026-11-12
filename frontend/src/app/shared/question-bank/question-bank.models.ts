import { UserSummary } from '../models/user-summary';

// Contract: docs/question-bank-api.md → DTO'lar

/** The question's subcategory: the project's request source. */
export type Subcategory = 'Internal' | 'External' | 'Affiliated';

/** SharedOptions: the set's options for every question; QuestionOptions: each question has its own (level). */
export type AnswerMode = 'SharedOptions' | 'QuestionOptions';

export type QuestionRequirement = 'Required' | 'Conditional';

/** What an option means, whatever its label on the form says. */
export type OptionMeaning = 'Complete' | 'Partial' | 'Open' | 'Negative' | 'NotApplicable';

export interface QuestionSet {
  code: string;
  name: string;
  sourceDocument: string;
  version: string;
  answerMode: AnswerMode;
}

export interface QuestionOption {
  id: number;
  code: string;
  label: string;
  points: number | null;
  meaning: OptionMeaning | null;
  /** Risk analysis "Risk Var" / "Belirsiz": probability and severity are entered. */
  isScored: boolean;
  /** The form expects a note with this answer (a warning, not a rule). */
  requiresNote: boolean;
}

export interface Question {
  id: number;
  code: string;
  no: number;
  section: string | null;
  group: string | null;
  topic: string | null;
  text: string;
  requirement: QuestionRequirement | null;
  expectedInfo: string | null;
  riskDescription: string | null;
  suggestedAction: string | null;
  reference: string | null;
  options: QuestionOption[];
}

export interface Answer {
  optionId: number | null;
  text: string | null;
  note: string | null;
  probability: number | null;
  severity: number | null;
  updatedBy: UserSummary | null;
  updatedAt: string | null;
}

export interface QuestionnaireItem {
  question: Question;
  /** Null when the question has not been answered. */
  answer: Answer | null;
}

export interface QuestionnaireSummary {
  listed: number;
  answered: number;
  requiredUnanswered: number;
  isComplete: boolean;
  byMeaning: Record<'complete' | 'partial' | 'open' | 'negative' | 'notApplicable', number>;
  missingNote: number;
}

export interface Questionnaire {
  projectId: number;
  set: QuestionSet;
  workType: string;
  subcategory: Subcategory;
  participants: string | null;
  /** yyyy-MM-dd */
  assessedOn: string | null;
  approvedBy: UserSummary | null;
  approvedOn: string | null;
  questions: QuestionnaireItem[];
  summary: QuestionnaireSummary;
  /** The form's own result (level, ratio, risk summary); filled by each form's own work. */
  result: unknown;
}

export interface SaveQuestionnaire {
  subcategory: Subcategory;
  participants: string | null;
  assessedOn: string | null;
  approvedByUserId: number | null;
  approvedOn: string | null;
}

/** Every field null empties the answer. */
export interface SaveAnswer {
  optionId: number | null;
  text: string | null;
  note: string | null;
  probability: number | null;
  severity: number | null;
}

export const SUBCATEGORY_LABELS: Record<Subcategory, string> = {
  Internal: 'Kurum içi',
  External: 'Dış (müşteri)',
  Affiliated: 'İlişkili şirket',
};

export const REQUIREMENT_LABELS: Record<QuestionRequirement, string> = {
  Required: 'Zorunlu',
  Conditional: 'Duruma bağlı',
};
