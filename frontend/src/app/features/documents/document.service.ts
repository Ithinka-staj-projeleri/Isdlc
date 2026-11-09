import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { DocumentType, ProjectDocument, SaveProjectDocument } from './models/document';

/** Documents API. Contract: docs/requirements-documents-api.md. The lists are short and not paged. */
@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly _http = inject(HttpClient);

  /** Every tracked document with its state in the project, in stage order. */
  list(projectId: number): Observable<ProjectDocument[]> {
    return this._http.get<ProjectDocument[]>(this.url(projectId));
  }

  /**
   * Saves the document's status, note and link (creates the record the first time).
   * The note dialog shows 400 field errors itself; the status select leaves them to the error interceptor.
   */
  save(projectId: number, documentTypeId: number, body: SaveProjectDocument, showsErrors = false): Observable<ProjectDocument> {
    return this._http.put<ProjectDocument>(
      `${this.url(projectId)}/${documentTypeId}`, body, showsErrors ? { context: showsFieldErrors() } : {});
  }

  /** The Document Library. */
  types(): Observable<DocumentType[]> {
    return this._http.get<DocumentType[]>('/api/v1/document-types');
  }

  private url(projectId: number): string {
    return `/api/v1/projects/${projectId}/documents`;
  }
}
