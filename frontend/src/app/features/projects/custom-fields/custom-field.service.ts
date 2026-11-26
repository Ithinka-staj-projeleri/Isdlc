import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../../core/http/problem-details';
import { CreateCustomFieldInput, CustomField, UpdateCustomFieldInput } from './custom-field';

/** Calls to /api/v1/custom-fields (Faz 7 · A6). Contract: docs/configurability.md → Şirkete özel alanlar. */
@Injectable({ providedIn: 'root' })
export class CustomFieldService {
  private readonly _http = inject(HttpClient);
  private readonly _baseUrl = '/api/v1/custom-fields';

  /** The active fields in form order; includeInactive (company administrators) adds the inactive ones. */
  list(includeInactive = false): Observable<CustomField[]> {
    const params = includeInactive ? new HttpParams().set('includeInactive', 'true') : undefined;
    return this._http.get<CustomField[]>(this._baseUrl, { params });
  }

  /** The form shows 400 field errors itself; 409 (key taken) comes as an alert. */
  create(input: CreateCustomFieldInput): Observable<CustomField> {
    return this._http.post<CustomField>(this._baseUrl, input, { context: showsFieldErrors() });
  }

  /** 409 when a removed option is used in a skip rule. */
  update(id: number, input: UpdateCustomFieldInput): Observable<CustomField> {
    return this._http.put<CustomField>(`${this._baseUrl}/${id}`, input, { context: showsFieldErrors() });
  }
}
