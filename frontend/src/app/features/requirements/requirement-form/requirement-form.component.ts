import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import {
  REQUIREMENT_PRIORITY_LABELS, REQUIREMENT_TYPE_LABELS, Requirement, RequirementPriority, RequirementType,
  SaveRequirement,
} from '../models/requirement';
import { RequirementService } from '../requirement.service';

type Field = 'type' | 'priority' | 'title' | 'description' | 'acceptanceCriteria';

const MAX_LENGTHS = { title: 300, description: 4000, acceptanceCriteria: 4000 } as const;

/** Add or edit a requirement. A new one starts as a draft; the status is changed from the list. */
@Component({
  selector: 'app-requirement-form',
  imports: [ReactiveFormsModule],
  templateUrl: './requirement-form.component.html',
  styleUrl: './requirement-form.component.scss',
})
export class RequirementFormComponent implements OnInit {
  private readonly _requirementService = inject(RequirementService);
  private readonly _fb = inject(FormBuilder);

  readonly projectId = input.required<number>();
  /** null adds a new record. */
  readonly requirementId = input<number | null>(null);

  readonly saved = output<Requirement>();
  readonly cancelled = output<void>();

  protected readonly typeLabels = REQUIREMENT_TYPE_LABELS;
  protected readonly priorityLabels = REQUIREMENT_PRIORITY_LABELS;
  protected readonly types = Object.keys(REQUIREMENT_TYPE_LABELS) as RequirementType[];
  protected readonly priorities = Object.keys(REQUIREMENT_PRIORITY_LABELS) as RequirementPriority[];
  protected readonly maxLengths = MAX_LENGTHS;

  protected readonly form = this._fb.nonNullable.group({
    type: this._fb.nonNullable.control<RequirementType>('Functional'),
    priority: this._fb.nonNullable.control<RequirementPriority>('ShouldHave'),
    title: [''],
    description: [''],
    acceptanceCriteria: [''],
  });

  /** The record's code while editing ("GR-003"). */
  protected readonly code = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  /** Messages from the API, by field name. */
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  ngOnInit(): void {
    const id = this.requirementId();
    if (id === null) {
      return;
    }

    this.loading.set(true);
    this._requirementService.get(this.projectId(), id).subscribe({
      next: (requirement) => {
        this.code.set(requirement.code);
        this.form.setValue({
          type: requirement.type,
          priority: requirement.priority,
          title: requirement.title,
          description: requirement.description ?? '',
          acceptanceCriteria: requirement.acceptanceCriteria ?? '',
        });
        this.loading.set(false);
      },
      // The error interceptor has already told the user.
      error: () => this.cancelled.emit(),
    });
  }

  /** The first message for a field: the API's first, then the form's own check after the first save. */
  protected errorFor(field: Field): string | null {
    const fromServer = this.serverErrors()[field];
    if (fromServer?.length) {
      return fromServer[0];
    }

    if (!this.submitted()) {
      return null;
    }

    const value = this.form.getRawValue();
    switch (field) {
      case 'title':
        if (!value.title.trim()) return 'Başlık zorunludur.';
        return value.title.length > MAX_LENGTHS.title ? 'Başlık en fazla 300 karakter olabilir.' : null;
      case 'description':
        return value.description.length > MAX_LENGTHS.description ? 'Açıklama en fazla 4000 karakter olabilir.' : null;
      case 'acceptanceCriteria':
        return value.acceptanceCriteria.length > MAX_LENGTHS.acceptanceCriteria
          ? 'Kabul ölçütleri en fazla 4000 karakter olabilir.'
          : null;
      default:
        return null;
    }
  }

  protected save(): void {
    this.submitted.set(true);
    this.serverErrors.set({});
    if (this.errorFor('title') || this.errorFor('description') || this.errorFor('acceptanceCriteria')) {
      return;
    }

    const value = this.form.getRawValue();
    const body: SaveRequirement = {
      type: value.type,
      priority: value.priority,
      title: value.title.trim(),
      description: value.description.trim() || null,
      acceptanceCriteria: value.acceptanceCriteria.trim() || null,
    };

    const id = this.requirementId();
    const request = id === null
      ? this._requirementService.create(this.projectId(), body)
      : this._requirementService.update(this.projectId(), id, body);

    this.saving.set(true);
    request.subscribe({
      next: (requirement) => {
        this.saving.set(false);
        this.saved.emit(requirement);
      },
      error: (err) => {
        this.saving.set(false);
        this.serverErrors.set(fieldErrorsOf(err));
      },
    });
  }
}
