import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { LookupService } from '../../../shared/services/lookup.service';
import { OPEN_ISSUE_TYPE_LABELS, OpenIssue, OpenIssueType, SaveOpenIssue } from '../models/open-issue';
import { OpenIssueService } from '../open-issue.service';

type OwnerKind = 'user' | 'name';

/** Add or edit an open issue. The owner is a team member or someone outside the system, never both. */
@Component({
    selector: 'app-open-issue-form',
    imports: [ReactiveFormsModule],
    templateUrl: './open-issue-form.component.html',
    styleUrl: './open-issue-form.component.scss',
})
export class OpenIssueFormComponent implements OnInit {
    private readonly openIssueService = inject(OpenIssueService);
    private readonly fb = inject(FormBuilder);

    readonly projectId = input.required<number>();
    /** null adds a new record. */
    readonly openIssueId = input<number | null>(null);

    readonly saved = output<OpenIssue>();
    readonly cancelled = output<void>();

    protected readonly typeLabels = OPEN_ISSUE_TYPE_LABELS;
    protected readonly types = Object.keys(OPEN_ISSUE_TYPE_LABELS) as OpenIssueType[];
    protected readonly users = toSignal(inject(LookupService).users$, { initialValue: [] });

    protected readonly form = this.fb.nonNullable.group({
        type: this.fb.nonNullable.control<OpenIssueType>('Action', Validators.required),
        title: ['', [Validators.required, Validators.maxLength(300)]],
        description: ['', Validators.maxLength(2000)],
        ownerKind: this.fb.nonNullable.control<OwnerKind>('user'),
        ownerUserId: this.fb.control<number | null>(null),
        ownerName: ['', Validators.maxLength(200)],
        dueDate: [''],
    });

    /** The record's code while editing ("AK-003"). */
    protected readonly code = signal<string | null>(null);
    protected readonly loading = signal(false);
    protected readonly saving = signal(false);
    protected readonly submitted = signal(false);
    /** Messages from the API, by field name. */
    protected readonly serverErrors = signal<Record<string, string[]>>({});

    ngOnInit(): void {
        const id = this.openIssueId();
        if (id === null) {
            return;
        }

        this.loading.set(true);
        this.openIssueService.get(this.projectId(), id).subscribe({
            next: issue => {
                this.code.set(issue.code);
                this.form.setValue({
                    type: issue.type,
                    title: issue.title,
                    description: issue.description ?? '',
                    ownerKind: issue.owner.userId === null ? 'name' : 'user',
                    ownerUserId: issue.owner.userId,
                    ownerName: issue.owner.userId === null ? issue.owner.name : '',
                    dueDate: issue.dueDate ?? '',
                });
                this.loading.set(false);
            },
            error: () => this.cancelled.emit(),
        });
    }

    /** The first message for a field: the form's own check first, then the API's. */
    protected errorFor(field: 'type' | 'title' | 'description' | 'owner' | 'dueDate'): string | null {
        const server = this.serverErrors();
        const fromServer = field === 'owner' ? server['ownerUserId'] ?? server['ownerName'] : server[field];
        if (fromServer?.length) {
            return fromServer[0];
        }

        if (!this.submitted()) {
            return null;
        }

        const value = this.form.getRawValue();
        switch (field) {
            case 'title':
                if (!value.title.trim()) return 'Konu zorunludur.';
                if (value.title.length > 300) return 'Konu en fazla 300 karakter olabilir.';
                return null;
            case 'description':
                return value.description.length > 2000 ? 'Açıklama en fazla 2000 karakter olabilir.' : null;
            case 'owner':
                if (value.ownerKind === 'user' && value.ownerUserId === null) return 'Sorumlu kişiyi seçin.';
                if (value.ownerKind === 'name' && !value.ownerName.trim()) return 'Sorumlunun adını yazın.';
                if (value.ownerName.length > 200) return 'Sorumlu adı en fazla 200 karakter olabilir.';
                return null;
            default:
                return null;
        }
    }

    protected save(): void {
        this.submitted.set(true);
        this.serverErrors.set({});
        if (this.errorFor('title') || this.errorFor('description') || this.errorFor('owner')) {
            return;
        }

        const value = this.form.getRawValue();
        const body: SaveOpenIssue = {
            type: value.type,
            title: value.title.trim(),
            description: value.description.trim() || null,
            ownerUserId: value.ownerKind === 'user' ? value.ownerUserId : null,
            ownerName: value.ownerKind === 'name' ? value.ownerName.trim() : null,
            dueDate: value.dueDate || null,
        };

        const id = this.openIssueId();
        const request = id === null
            ? this.openIssueService.create(this.projectId(), body)
            : this.openIssueService.update(this.projectId(), id, body);

        this.saving.set(true);
        request.subscribe({
            next: issue => {
                this.saving.set(false);
                this.saved.emit(issue);
            },
            error: err => {
                this.saving.set(false);
                this.serverErrors.set(fieldErrorsOf(err));
            },
        });
    }
}
