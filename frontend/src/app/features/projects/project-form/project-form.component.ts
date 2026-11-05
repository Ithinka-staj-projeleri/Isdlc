import { Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { UserLookup, UserSummary } from '../../../shared/models/user-summary';
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

/** Adds the currently assigned person to a picker list if they are not in it (e.g. deactivated since). */
function withAssigned(users: UserLookup[], assigned: UserSummary | null | undefined): UserSummary[] {
  return assigned && !users.some((user) => user.id === assigned.id) ? [assigned, ...users] : users;
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
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _projectService = inject(ProjectService);

  readonly sourceOptions = Object.entries(REQUEST_SOURCE_LABELS) as [RequestSource, string][];
  readonly channelOptions = Object.entries(REQUEST_CHANNEL_LABELS) as [RequestChannel, string][];
  readonly typeOptions = Object.entries(REQUEST_TYPE_LABELS) as [RequestType, string][];

  /** Set on /projects/:projectId/edit; absent on /requests/new. */
  readonly projectId = toSignal(
    this._route.paramMap.pipe(map((params) => (params.has('projectId') ? Number(params.get('projectId')) : null))),
    { requireSync: true },
  );
  readonly isEdit = computed(() => this.projectId() !== null);

  /** The project being edited. Not loaded for a new request (params undefined = idle). */
  private readonly _existing = rxResource({
    params: () => this.projectId() ?? undefined,
    stream: ({ params }) => this._projectService.get(params),
  });

  // A resource throws when its value is read in the error state (e.g. the API is down),
  // which would stop the whole screen from updating. Read it only when there is a value.
  readonly existing = computed(() => (this._existing.hasValue() ? this._existing.value() : undefined));
  readonly loadFailed = computed(() => !!this._existing.error());

  /** A closed, rejected, lost or redirected project keeps its information (API: 409). */
  readonly locked = computed(() => !!this.existing() && this.existing()!.status !== 'Active');

  private readonly _analysts = rxResource({ stream: () => this._projectService.users('BusinessAnalyst') });
  private readonly _managers = rxResource({ stream: () => this._projectService.users('ProjectManager') });

  // The assigned person stays in the list even if they were deactivated since, so the form does not
  // silently show "Atanmadı" for them.
  readonly analysts = computed(() =>
    withAssigned(this._analysts.hasValue() ? this._analysts.value() : [], this.existing()?.businessAnalyst),
  );
  readonly managers = computed(() =>
    withAssigned(this._managers.hasValue() ? this._managers.value() : [], this.existing()?.projectManager),
  );

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

  constructor() {
    // Edit: fill the form once the project has loaded; lock it if the project is no longer active.
    effect(() => {
      const project = this.existing();
      if (!project) {
        return;
      }
      this.form.reset({
        name: project.name,
        description: project.description,
        requestType: project.requestType,
        source: project.source,
        requestChannel: project.requestChannel,
        customerName: project.customerName,
        requesterName: project.requesterName,
        requiresNda: project.requiresNda,
        businessAnalystId: project.businessAnalyst?.id ?? null,
        projectManagerId: project.projectManager?.id ?? null,
        startDate: project.startDate,
        dueDate: project.dueDate,
      });
      if (project.status !== 'Active') {
        this.form.disable();
      }
    });
  }

  /** Where "İptal" and the back link go: the project when editing, the request list otherwise. */
  readonly backLink = computed(() =>
    this.isEdit() ? { path: ['/projects', this.projectId()], label: 'Projeye dön' } : { path: ['/requests'], label: 'Talepler' },
  );

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
    if (this.form.invalid || this.saving() || this.locked()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const projectId = this.projectId();
    const request =
      projectId === null
        ? this._projectService.create(this.toInput())
        : this._projectService.update(projectId, this.toInput());

    request.subscribe({
      next: (saved) => this._router.navigate(['/projects', saved.id]),
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
