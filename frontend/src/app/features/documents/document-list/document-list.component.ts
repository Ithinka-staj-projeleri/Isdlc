import { Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { DocumentNoteFormComponent } from '../document-note-form/document-note-form.component';
import { DocumentService } from '../document.service';
import {
  DOCUMENT_STATUS_LABELS, DOCUMENT_STATUS_TONES, DocumentStatus, ProjectDocument,
} from '../models/document';

/** The documents of one stage, as shown under the stage heading. */
interface StageGroup {
  stageOrder: number;
  stageShortTitle: string;
  documents: ProjectDocument[];
}

/**
 * The project's documents grouped by stage: status per document (saved as soon as it is chosen),
 * note and link in a small dialog. Read-only when the project is not active.
 */
@Component({
  selector: 'app-document-list',
  imports: [FormsModule, EmptyStateComponent, StatusBadgeComponent, LocalDatePipe, DocumentNoteFormComponent],
  templateUrl: './document-list.component.html',
  styleUrl: './document-list.component.scss',
})
export class DocumentListComponent {
  private readonly _documentService = inject(DocumentService);

  readonly projectId = input.required<number>();

  /** True when the project is not active: documents can be read but not changed. */
  readonly readOnly = input(false);

  protected readonly statusLabels = DOCUMENT_STATUS_LABELS;
  protected readonly statusTones = DOCUMENT_STATUS_TONES;
  protected readonly statuses = Object.keys(DOCUMENT_STATUS_LABELS) as DocumentStatus[];

  protected readonly documents = rxResource({
    params: () => this.projectId(),
    stream: ({ params }) => this._documentService.list(params),
  });

  /** Only for the short descriptions; the list works without them. */
  private readonly _types = rxResource({ stream: () => this._documentService.types() });

  // A resource throws when its value is read in the error state, so check hasValue() first.
  private readonly _descriptions = computed(() =>
    new Map((this._types.hasValue() ? this._types.value() : []).map((t) => [t.id, t.description])));

  protected readonly groups = computed<StageGroup[]>(() => {
    const groups: StageGroup[] = [];
    for (const doc of this.documents.hasValue() ? this.documents.value() : []) {
      const last = groups.at(-1);
      if (last?.stageOrder === doc.documentType.stageOrder) {
        last.documents.push(doc);
      } else {
        groups.push({
          stageOrder: doc.documentType.stageOrder,
          stageShortTitle: doc.documentType.stageShortTitle,
          documents: [doc],
        });
      }
    }
    return groups;
  });

  /** Approved out of the documents the project needs (not counting NotRequired); the bar shows the same. */
  protected readonly summary = computed(() => {
    const docs = this.documents.hasValue() ? this.documents.value() : [];
    const approved = docs.filter((d) => d.status === 'Approved').length;
    const notRequired = docs.filter((d) => d.status === 'NotRequired').length;
    const needed = docs.length - notRequired;
    return {
      total: docs.length,
      approved,
      notRequired,
      percent: needed === 0 ? 100 : Math.round((approved / needed) * 100),
    };
  });

  /** The document whose note dialog is open. */
  protected readonly editing = signal<ProjectDocument | null>(null);

  /** The row whose status is being saved, so its select is disabled meanwhile. */
  protected readonly busyId = signal<number | null>(null);

  protected description(documentTypeId: number): string | null {
    return this._descriptions().get(documentTypeId) ?? null;
  }

  protected changeStatus(doc: ProjectDocument, status: DocumentStatus): void {
    if (status === doc.status) {
      return;
    }

    const typeId = doc.documentType.id;
    this.busyId.set(typeId);
    this._documentService.save(this.projectId(), typeId, { status, note: doc.note, link: doc.link }).subscribe({
      next: () => {
        this.busyId.set(null);
        this.documents.reload();
      },
      // The error interceptor tells the user; reloading puts the select back to the saved status.
      error: () => {
        this.busyId.set(null);
        this.documents.reload();
      },
    });
  }

  protected onSaved(): void {
    this.editing.set(null);
    this.documents.reload();
  }
}
