import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { silentErrors } from '../../core/http/problem-details';
import { SearchResult } from './models/search';

/** The search box in the top bar. Contract: docs/search-api.md */
@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/search';

  /** Asked while typing, so a failure opens no alert; the panel says so itself. */
  search(text: string): Observable<SearchResult> {
    return this.http.get<SearchResult>(this.url, { params: new HttpParams().set('q', text), context: silentErrors() });
  }
}
