import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { REQUEST_CHANNEL_LABELS, REQUEST_SOURCE_LABELS, REQUEST_TYPE_LABELS } from '../project-labels';
import { ProjectInput, RequestChannel, RequestSource, RequestType } from '../project.model';
import { ProjectService } from '../project.service';

/** Required, and only spaces does not count as filled in (same as the API). */
const notBlank: ValidatorFn = (control) =>
  typeof control.value === 'string' && control.value.trim() === '' ? { required: true } : null;

/** "Bitiş tarihi başlangıç tarihinden önce olamaz." Dates are "yyyy-MM-dd", so text comparison works. */
const dueNotBeforeStart: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const start = group.get('startDate')?.value as string | null;
  const due = group.get('dueDate')?.value as string | null;
  return start && due && due < start ? { dueBeforeStart: true } : null;
};

/** Today in the user's time zone as "yyyy-MM-dd" (the form suggests it as the start date). */
function today(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** Turkish messages for the client-side checks; the same texts the API uses. */
const MESSAGES: Record<string, Record<string, string>> = {
  name: { required: 'Proje adı zorunludur.', maxlength: 'Proje adı en fazla 200 karakter olabilir.' },
  description: { required: 'Açıklama zorunludur.', maxlength: 'Açıklama en fazla 2000 karakter olabilir.' },
  source: { required: 'Talep kaynağı zorunludur.' },
  requestChannel: { required: 'Talep kanalı zorunludur.' },
  requestType: { required: 'Talep türü zorunludur.' },
  customerName: { required: 'Müşteri adı zorunludur.', maxlength: 'Müşteri adı en fazla 200 karakter olabilir.' },
  requesterName: { required: 'Talep sahibi zorunludur.', maxlength: 'Talep sahibi en fazla 200 karakter olabilir.' },
  startDate: { required: 'Başlangıç tarihi zorunludur.' },
};

/**
 * The request form ("Yeni talep"). Contract: docs/projects-api.md → CreateProjectDto.
 * Rules are checked here first; the API checks them again and its 400 messages appear under the same fields.
 */
@Component({
  selector: 'app-project-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './project-form.component.html',
  styleUrl: './project-form.component.scss',
})
export class ProjectFormComponent {
  private readonly _fb = inject(FormBuilder);
  private readonly _router = inject(Router);
  private readonly _projectService = inject(ProjectService);

  readonly sourceOptions = Object.entries(REQUEST_SOURCE_LABELS) as [RequestSource, string][];
  readonly channelOptions = Object.entries(REQUEST_CHANNEL_LABELS) as [RequestChannel, string][];
  readonly typeOptions = Object.entries(REQUEST_TYPE_LABELS) as [RequestType, string][];

  private readonly _analysts = rxResource({ stream: () => this._projectService.users('BusinessAnalyst') });
  private readonly _managers = rxResource({ stream: () => this._projectService.users('ProjectManager') });

  // A resource throws when its value is read in the error state (e.g. the API is down),
  // which would stop the whole screen from updating. Read it only when there is a value.
  readonly analysts = computed(() => (this._analysts.hasValue() ? this._analysts.value() : []));
  readonly managers = computed(() => (this._managers.hasValue() ? this._managers.value() : []));

  readonly form = this._fb.group(
    {
      name: this._fb.nonNullable.control('', [Validators.required, notBlank, Validators.maxLength(200)]),
      description: this._fb.nonNullable.control('', [Validators.required, notBlank, Validators.maxLength(2000)]),
      requestType: this._fb.control<RequestType | null>(null, Validators.required),
      source: this._fb.control<RequestSource | null>(null, Validators.required),
      requestChannel: this._fb.control<RequestChannel | null>(null, Validators.required),
      customerName: this._fb.nonNullable.control('', [Validators.required, notBlank, Validators.maxLength(200)]),
      requesterName: this._fb.nonNullable.control('', [Validators.required, notBlank, Validators.maxLength(200)]),
      requiresNda: this._fb.nonNullable.control(false),
      businessAnalystId: this._fb.control<number | null>(null),
      projectManagerId: this._fb.control<number | null>(null),
      startDate: this._fb.control<string | null>(today(), Validators.required),
      dueDate: this._fb.control<string | null>(null),
    },
    { validators: dueNotBeforeStart },
  );

  readonly saving = signal(false);
  private readonly _submitted = signal(false);

  /** The message to show under a field, or null. Errors appear after the field is left or the form is sent. */
  errorFor(field: keyof typeof this.form.controls): string | null {
    const control = this.form.controls[field];
    if (!control.errors || !(control.touched || this._submitted())) {
      return null;
    }

    // A message from the API wins: it knows rules the screen cannot check.
    if (control.errors['server']) {
      return control.errors['server'] as string;
    }

    const firstError = Object.keys(control.errors)[0];
    return MESSAGES[field]?.[firstError] ?? 'Geçersiz değer.';
  }

  /** The two-date rule belongs to the form, but it is shown under the due date. */
  dueDateError(): string | null {
    const due = this.form.controls.dueDate;
    if (due.errors?.['server']) {
      return due.errors['server'] as string;
    }
    return this.form.errors?.['dueBeforeStart'] && (due.touched || this._submitted())
      ? 'Bitiş tarihi başlangıç tarihinden önce olamaz.'
      : null;
  }

  save(): void {
    this._submitted.set(true);
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this._projectService.create(this.toInput()).subscribe({
      next: (created) => this._router.navigate(['/projects', created.id]),
      error: (error: unknown) => {
        this.saving.set(false);
        this.showServerErrors(fieldErrorsOf(error));
      },
    });
  }

  /** Puts each 400 message from the API under its field; typing in the field clears it again. */
  private showServerErrors(errors: Record<string, string[]>): void {
    for (const [field, messages] of Object.entries(errors)) {
      const control = this.form.get(field);
      if (control && messages.length > 0) {
        control.setErrors({ ...control.errors, server: messages[0] });
        control.markAsTouched();
      }
    }
  }

  private toInput(): ProjectInput {
    const value = this.form.getRawValue();
    return {
      name: value.name.trim(),
      description: value.description.trim(),
      requestType: value.requestType!,
      source: value.source!,
      requestChannel: value.requestChannel!,
      customerName: value.customerName.trim(),
      requesterName: value.requesterName.trim(),
      requiresNda: value.requiresNda,
      // A <select> gives text; the API wants a number or null.
      businessAnalystId: value.businessAnalystId ? Number(value.businessAnalystId) : null,
      projectManagerId: value.projectManagerId ? Number(value.projectManagerId) : null,
      startDate: value.startDate!,
      dueDate: value.dueDate || null,
    };
  }
}
