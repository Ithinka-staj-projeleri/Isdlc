import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { UserRole } from '../../shared/models/user-role';

/** The signed-in user's company (backend: TenantSummaryDto). */
export interface AuthTenant {
    id: number;
    code: string;
    name: string;
}

/** Signed-in user (backend: AuthUserDto). */
export interface AuthUser {
    id: number;
    fullName: string;
    email: string;
    role: UserRole;
    /** null for the Platform Administrator. Missing in a session saved before Faz 6. */
    tenant?: AuthTenant | null;
    isTenantAdmin?: boolean;
}

/** POST /api/v1/auth/login and /auth/change-password answer (backend: LoginResultDto). */
export interface LoginResponse {
    token: string;
    /** UTC, ISO 8601. */
    expiresAt: string;
    user: AuthUser;
    /** Signed in with a temporary password: every page but /change-password is closed until it is changed. */
    mustChangePassword?: boolean;
}

export const LOGIN_URL = '/api/v1/auth/login';
export const CHANGE_PASSWORD_URL = '/api/v1/auth/change-password';

/** Where the Platform Administrator works; it is the only page they see (docs/multi-tenancy.md → Ekranlar). */
export const PLATFORM_HOME = '/platform/tenants';

const SESSION_KEY = 'isdlc_session';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private _http = inject(HttpClient);
    private _router = inject(Router);

    private _session = signal<LoginResponse | null>(readSession());

    readonly user = computed(() => this._session()?.user ?? null);
    readonly mustChangePassword = computed(() => !!this._session()?.mustChangePassword);
    readonly isPlatformAdmin = computed(() => this.user()?.role === 'PlatformAdmin');
    readonly isTenantAdmin = computed(() => !!this.user()?.isTenantAdmin);

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

    /** First page after sign-in: the password change, the company list (Platform Administrator) or the dashboard. */
    homeUrl(): string {
        return homeUrlFor(this._session());
    }

    login(email: string, password: string): Observable<LoginResponse> {
        return this._http
            .post<LoginResponse>(LOGIN_URL, { email, password })
            .pipe(tap(res => this.saveSession(res)));
    }

    /** The answer carries a new token without the temporary password mark; it replaces the session. */
    changePassword(currentPassword: string, newPassword: string, context?: HttpContext): Observable<LoginResponse> {
        return this._http
            .post<LoginResponse>(CHANGE_PASSWORD_URL, { currentPassword, newPassword }, { context })
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

/** Pure, so the rule is tested without a browser. */
export function homeUrlFor(session: Pick<LoginResponse, 'user' | 'mustChangePassword'> | null): string {
    if (session?.mustChangePassword) {
        return '/change-password';
    }
    return session?.user.role === 'PlatformAdmin' ? PLATFORM_HOME : '/dashboard';
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
