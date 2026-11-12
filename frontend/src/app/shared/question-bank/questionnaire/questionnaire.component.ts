import { Component, computed, inject, input, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, tap } from 'rxjs';
import { EmptyStateComponent } from '../../components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../components/status-badge/status-badge.component';
import { LocalDatePipe } from '../../pipes/local-date-pipe';
import {
  QuestionnaireItem, Questionnaire, REQUIREMENT_LABELS, SUBCATEGORY_LABELS, SaveAnswer, SaveQuestionnaire, Subcategory,
} from '../question-bank.models';
import { QuestionnaireService } from '../questionnaire.service';
import {
  chosenOption, countOf, degreeOf, groupQuestions, headerWith, isMissingNote, withLevel, withOption, withText,
} from '../questionnaire-view';

/**
 * One of the company's forms filled in on a project (docs/question-bank-api.md): the header, a summary strip and the
 * questions split into sections and groups. Every change is saved at once, row by row, like the Excel form; the whole
 * form comes back with its new summary. Used by the level, request analysis, risk analysis and internal approval tabs;
 * each tab shows its own result next to it (the `changed` output gives it the saved form).
 */
@Component({
  selector: 'app-questionnaire',
  imports: [EmptyStateComponent, StatusBadgeComponent, LocalDatePipe],
  templateUrl: './questionnaire.component.html',
  styleUrl: './questionnaire.component.scss',
})
export class QuestionnaireComponent {
  private readonly questionnaireService = inject(QuestionnaireService);

  readonly projectId = input.required<number>();

  /** "level", "request-analysis", "risk-analysis", "internal-approval". */
  readonly setCode = input.required<string>();

  /** True when the project is not active: the form can be read but not changed. */
  readonly readOnly = input(false);

  /** Names for the form's section codes, e.g. level: { '1': 'Doküman seviyesi' }; the code is shown otherwise. */
  readonly sectionTitles = input<Record<string, string>>({});

  /** The form after it was loaded or saved, for the tab's own result. */
  readonly changed = output<Questionnaire>();

  protected readonly subcategoryLabels = SUBCATEGORY_LABELS;
  protected readonly subcategories = Object.keys(SUBCATEGORY_LABELS) as Subcategory[];
  protected readonly requirementLabels = REQUIREMENT_LABELS;
  protected readonly levels = [1, 2, 3];

  protected readonly form = rxResource({
    params: () => ({ projectId: this.projectId(), setCode: this.setCode() }),
    // The tab gets the form as soon as it is loaded, so it can show its result without a request of its own.
    stream: ({ params }) =>
      this.questionnaireService.get(params.projectId, params.setCode).pipe(tap(form => this.changed.emit(form))),
  });

  /** Only the questions still waiting for an option. */
  protected readonly onlyOpen = signal(false);

  /** The question being saved (or 0 for the header), so that row cannot be clicked twice meanwhile. */
  protected readonly savingId = signal<number | null>(null);

  protected readonly sections = computed(() => {
    const form = this.form.value();
    if (!form) return [];
    const items = this.onlyOpen() ? form.questions.filter(i => !chosenOption(i)) : form.questions;
    return groupQuestions(items);
  });

  /** Request analysis records the stakeholder's own answer; the other forms only choose and note. */
  protected readonly hasText = computed(() => this.form.value()?.questions.some(i => i.question.expectedInfo) ?? false);

  protected readonly chosenOption = chosenOption;
  protected readonly countOf = countOf;
  protected readonly degreeOf = degreeOf;
  protected readonly isMissingNote = isMissingNote;

  protected choose(item: QuestionnaireItem, optionId: number): void {
    this.saveAnswer(item, withOption(item, optionId));
  }

  protected setLevel(item: QuestionnaireItem, field: 'probability' | 'severity', value: number): void {
    this.saveAnswer(item, withLevel(item, field, value));
  }

  /** Saved when the field is left, and only if it changed. */
  protected editText(item: QuestionnaireItem, field: 'text' | 'note', value: string): void {
    const next = withText(item, field, value);
    if (next[field] === (item.answer?.[field] ?? null)) return;
    this.saveAnswer(item, next);
  }

  protected setSubcategory(value: string): void {
    this.saveHeader({ subcategory: value as Subcategory });
  }

  protected setParticipants(value: string): void {
    const participants = value.trim() === '' ? null : value.trim();
    if (participants === (this.form.value()?.participants ?? null)) return;
    this.saveHeader({ participants });
  }

  protected setAssessedOn(value: string): void {
    this.saveHeader({ assessedOn: value === '' ? null : value });
  }

  protected toggleOnlyOpen(): void {
    this.onlyOpen.update(v => !v);
  }

  protected isSaving(questionId: number): boolean {
    return this.savingId() === questionId;
  }

  private saveAnswer(item: QuestionnaireItem, answer: SaveAnswer): void {
    this.save(item.question.id, this.questionnaireService.saveAnswer(this.projectId(), this.setCode(), item.question.id, answer));
  }

  private saveHeader(change: Partial<SaveQuestionnaire>): void {
    const form = this.form.value();
    if (!form) return;
    this.save(0, this.questionnaireService.save(this.projectId(), this.setCode(), headerWith(form, change)));
  }

  private save(id: number, request: Observable<Questionnaire>): void {
    this.savingId.set(id);
    request.subscribe({
      next: result => {
        this.form.set(result);
        this.savingId.set(null);
        this.changed.emit(result);
      },
      // The error interceptor shows the message; reload so the screen shows what is really saved.
      error: () => {
        this.savingId.set(null);
        this.form.reload();
      },
    });
  }
}
