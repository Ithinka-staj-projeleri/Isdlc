import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { UpdateMyProfile, UserProfile, UserWorkload } from './models/user-profile';

/** Kişi profili and the owner selector (Faz 5 · F9). Contract: docs/users-api.md */
@Injectable({ providedIn: 'root' })
export class UserProfileService {
  private readonly _http = inject(HttpClient);

  get(userId: number): Observable<UserProfile> {
    return this._http.get<UserProfile>(`/api/v1/users/${userId}/profile`);
  }

  updateMine(body: UpdateMyProfile): Observable<UserProfile> {
    return this._http.put<UserProfile>('/api/v1/users/me/profile', body, { context: showsFieldErrors() });
  }

  /** Not cached: the counts change as open issues are saved. */
  workload(): Observable<UserWorkload[]> {
    return this._http.get<UserWorkload[]>('/api/v1/users/workload');
  }
}
