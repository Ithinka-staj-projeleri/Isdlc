import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, of, tap, throwError } from 'rxjs';

export type UserRole =
    | 'BusinessAnalyst'
    | 'SoftwareDepartmentManager'
    | 'ProjectManager'
    | 'SoftwareDeveloper'
    | 'TestSpecialist'
    | 'SalesRepresentative';

export const ROLE_LABELS: Record<UserRole, string> = {
    BusinessAnalyst: 'İş Analisti',
    SoftwareDepartmentManager: 'Yazılım Departman Yöneticisi',
    ProjectManager: 'Proje Yöneticisi',
    SoftwareDeveloper: 'Yazılım Geliştirici',
    TestSpecialist: 'Test Uzmanı',
    SalesRepresentative: 'Satış Temsilcisi',
};

export interface AuthUser {
    id: number;
    fullName: string;
    email: string;
    role: UserRole;
}

export interface LoginResponse {
    token: string;
    user: AuthUser;
}

// Backend hazır olunca false yapılacak
const USE_MOCK = true;

const TOKEN_KEY = 'isdlc_token';
const USER_KEY = 'isdlc_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private _http = inject(HttpClient);
    private _router = inject(Router);

    private _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
    private _user = signal<AuthUser | null>(this.readUser());

    readonly token = this._token.asReadonly();
    readonly user = this._user.asReadonly();
    readonly isLoggedIn = computed(() => !!this._token());

    login(email: string, password: string): Observable<LoginResponse> {
        const request$ = USE_MOCK
            ? this.mockLogin(email, password)
            : this._http.post<LoginResponse>('/api/v1/auth/login', { email, password });

        return request$.pipe(tap(res => this.saveSession(res)));
    }

    logout(): void {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
        this._token.set(null);
        this._user.set(null);
        this._router.navigate(['/login']);
    }

    hasRole(...roles: UserRole[]): boolean {
        const user = this._user();
        return !!user && roles.includes(user.role);
    }

    private saveSession(res: LoginResponse): void {
        localStorage.setItem(TOKEN_KEY, res.token);
        localStorage.setItem(USER_KEY, JSON.stringify(res.user));
        this._token.set(res.token);
        this._user.set(res.user);
    }

    private readUser(): AuthUser | null {
        try {
            const raw = localStorage.getItem(USER_KEY);
            return raw ? (JSON.parse(raw) as AuthUser) : null;
        } catch {
            return null;
        }
    }

    private mockLogin(email: string, password: string): Observable<LoginResponse> {
        if (email === 'test@isdlc.local' && password === '123456') {
            return of({
                token: 'mock-token',
                user: { id: 1, fullName: 'Test Kullanıcı', email, role: 'BusinessAnalyst' },
            });
        }
        return throwError(() => new Error('E-posta veya şifre hatalı.'));
    }
}