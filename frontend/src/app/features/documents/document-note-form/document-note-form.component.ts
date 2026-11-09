import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { DocumentService } from '../document.service';
import { DOCUMENT_STATUS_LABELS, DocumentStatus, ProjectDocument } from '../models/document';

const MAX_LENGTH = 1000;

/** Edits a document's status, note and link in a small dialog. */
@Component({
  selector: 'app-document-note-form',
  imports: [ReactiveFormsModule],
  templateUrl: './document-note-form.component.html',
  styleUrl: './document-note-form.component.scss',
})
export class DocumentNoteFormComponent implements OnInit {
  private readonly _documentService = inject(DocumentService);
  private readonly _fb = inject(FormBuilder);

  readonly projectId = input.required<number>();
  readonly document = input.required<ProjectDocument>();

  readonly saved = output<ProjectDocument>();
  readonly cancelled = output<void>();

  protected readonly statusLabels = DOCUMENT_STATUS_LABELS;
  protected readonly statuses = Object.keys(DOCUMENT_STATUS_LABELS) as DocumentStatus[];
  protected readonly maxLength = MAX_LENGTH;

  protected readonly form = this._fb.nonNullable.group({
    status: this._fb.nonNullable.control<DocumentStatus>('NotStarted'),
    note: [''],
    link: [''],
  });

  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  /** Messages from the API, by field name. */
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  ngOnInit(): void {
    const doc = this.document();
    this.form.setValue({ status: doc.status, note: doc.note ?? '', link: doc.link ?? '' });
  }

  /** The first message for a field: the API's first, then the form's own check after the first save. */
  protected errorFor(field: 'status' | 'note' | 'link'): string | null {
    const fromServer = this.serverErrors()[field];
    if (fromServer?.length) {
      return fromServer[0];
    }

    if (!this.submitted()) {
      return null;
    }

    const value = this.form.getRawValue();
    switch (field) {
      case 'note':
        return value.note.length > MAX_LENGTH ? 'Not en fazla 1000 karakter olabilir.' : null;
      case 'link': {
        const link = value.link.trim();
        if (link.length > MAX_LENGTH) return 'Bağlantı en fazla 1000 karakter olabilir.';
        return link && !/^https?:\/\/\S+$/i.test(link)
          ? 'Bağlantı http:// ya da https:// ile başlayan bir adres olmalıdır.'
          : null;
      }
      default:
        return null;
    }
  }

  protected save(): void {
    this.submitted.set(true);
    this.serverErrors.set({});
    if (this.errorFor('note') || this.errorFor('link')) {
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this._documentService
      .save(
        this.projectId(),
        this.document().documentType.id,
        { status: value.status, note: value.note.trim() || null, link: value.link.trim() || null },
        true,
      )
      .subscribe({
        next: (doc) => {
          this.saving.set(false);
          this.saved.emit(doc);
        },
        error: (err) => {
          this.saving.set(false);
          this.serverErrors.set(fieldErrorsOf(err));
        },
      });
  }
}
