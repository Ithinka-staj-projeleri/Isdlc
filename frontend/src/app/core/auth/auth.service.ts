import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { UserRole } from '../../shared/models/user-role';

/** Signed-in user (backend: AuthUserDto). */
export interface AuthUser {
    id: number;
    fullName: string;
    email: string;
    role: UserRole;
}

/** POST /api/v1/auth/login answer (backend: LoginResultDto). */
export interface LoginResponse {
    token: string;
    /** UTC, ISO 8601. */
    expiresAt: string;
    user: AuthUser;
}

export const LOGIN_URL = '/api/v1/auth/login';

const SESSION_KEY = 'isdlc_session';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private _http = inject(HttpClient);
    private _router = inject(Router);

    private _session = signal<LoginResponse | null>(readSession());

    readonly user = computed(() => this._session()?.user ?? null);

    /** Token to send to the API; null when signed out or when the token has expired. */
    token(): string | null {
        return this.isLoggedIn() ? this._session()!.token : null;
    }

    isLoggedIn(): boolean {
        const session = this._session();
        if (!session) {
            return false;
        }
        if (isExpired(session)) {
            this.clearSession();
            return false;
        }
        return true;
    }

    login(email: string, password: string): Observable<LoginResponse> {
        return this._http
            .post<LoginResponse>(LOGIN_URL, { email, password })
            .pipe(tap(res => this.saveSession(res)));
    }

    /** Ends the session and opens the login page; `returnUrl` is opened again after the next sign-in. */
    logout(returnUrl?: string): void {
        this.clearSession();
        this._router.navigate(['/login'], returnUrl ? { queryParams: { returnUrl } } : {});
    }

    hasRole(...roles: UserRole[]): boolean {
        const user = this.user();
        return !!user && roles.includes(user.role);
    }

    private saveSession(res: LoginResponse): void {
        try {
            localStorage.setItem(SESSION_KEY, JSON.stringify(res));
        } catch {
            // Storage blocked (private window): the session still works until the page is reloaded.
        }
        this._session.set(res);
    }

    private clearSession(): void {
        try {
            localStorage.removeItem(SESSION_KEY);
        } catch {
            // Nothing stored.
        }
        this._session.set(null);
    }
}

function isExpired(session: LoginResponse): boolean {
    const expiresAt = Date.parse(session.expiresAt);
    return Number.isNaN(expiresAt) || expiresAt <= Date.now();
}

function readSession(): LoginResponse | null {
    try {
        const raw = localStorage.getItem(SESSION_KEY);
        return raw ? (JSON.parse(raw) as LoginResponse) : null;
    } catch {
        return null;
    }
}
