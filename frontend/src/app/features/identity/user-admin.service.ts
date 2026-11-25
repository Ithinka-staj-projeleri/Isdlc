import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { CreateUserInput, ManagedUser, UpdateUserInput } from './models/managed-user';

/** The company administrator's user management. Contract: docs/multi-tenancy.md → Uç noktalar, Şirket Yöneticisi. */
@Injectable({ providedIn: 'root' })
export class UserAdminService {
    private readonly http = inject(HttpClient);
    private readonly url = '/api/v1/users';

    /** Every user of the company, inactive ones too. */
    list(): Observable<ManagedUser[]> {
        return this.http.get<ManagedUser[]>(`${this.url}/manage`);
    }

    create(input: CreateUserInput): Observable<ManagedUser> {
        return this.http.post<ManagedUser>(this.url, input, { context: showsFieldErrors() });
    }

    update(id: number, input: UpdateUserInput): Observable<ManagedUser> {
        return this.http.put<ManagedUser>(`${this.url}/${id}`, input, { context: showsFieldErrors() });
    }

    /** The user signs in with it and has to change it. */
    resetPassword(id: number, temporaryPassword: string): Observable<void> {
        return this.http.post<void>(`${this.url}/${id}/reset-password`, { temporaryPassword }, { context: showsFieldErrors() });
    }
}
