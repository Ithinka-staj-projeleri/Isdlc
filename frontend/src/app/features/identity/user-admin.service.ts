import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { PermissionCatalog } from '../../shared/models/permission';
import { CreateUserInput, ManagedUser, UpdateUserInput } from './models/managed-user';

/**
 * The company administrator's user management. Contracts: docs/multi-tenancy.md → Şirket Yöneticisi;
 * docs/configurability.md → Yetkiler (Faz 7).
 */
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

    /** The permission list and the ready-made sets (GET /api/v1/permissions), so the form keeps no list of its own. */
    catalog(): Observable<PermissionCatalog> {
        return this.http.get<PermissionCatalog>('/api/v1/permissions');
    }
}
