import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { showsFieldErrors } from '../../core/http/problem-details';
import { permissionsFromRole, roleFromPermissions } from '../../shared/models/user-role';
import { CreateUserInput, ManagedUser, UpdateUserInput } from './models/managed-user';

/** The company administrator's user management. Contract: docs/multi-tenancy.md → Uç noktalar, Şirket Yöneticisi. */
@Injectable({ providedIn: 'root' })
export class UserAdminService {
    private readonly http = inject(HttpClient);
    private readonly url = '/api/v1/users';

    /** Every user of the company, inactive ones too. */
    list(): Observable<ManagedUser[]> {
        return this.http.get<ManagedUser[]>(`${this.url}/manage`).pipe(map(users => users.map(withRole)));
    }

    create(input: CreateUserInput): Observable<ManagedUser> {
        return this.http
            .post<ManagedUser>(this.url, { ...input, permissions: permissionsFromRole(input.role) }, { context: showsFieldErrors() })
            .pipe(map(withRole));
    }

    update(id: number, input: UpdateUserInput): Observable<ManagedUser> {
        return this.http
            .put<ManagedUser>(`${this.url}/${id}`, { ...input, permissions: permissionsFromRole(input.role) }, { context: showsFieldErrors() })
            .pipe(map(withRole));
    }

    /** The user signs in with it and has to change it. */
    resetPassword(id: number, temporaryPassword: string): Observable<void> {
        return this.http.post<void>(`${this.url}/${id}/reset-password`, { temporaryPassword }, { context: showsFieldErrors() });
    }
}

/** A2 bridge (removed by A3): the list shows a title worked out from the permissions. */
function withRole(user: ManagedUser): ManagedUser {
    return { ...user, role: roleFromPermissions(user.permissions) };
}
