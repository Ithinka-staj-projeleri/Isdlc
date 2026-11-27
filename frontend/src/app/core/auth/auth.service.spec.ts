import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { errorInterceptor } from '../interceptors/error.interceptor';
import { AuthService, AuthUser, LOGIN_URL, LoginResponse, ME_URL, homeUrlFor } from './auth.service';

/**
 * Cross tests (PLANNING.md): Erva → Baran's A3. Permissions are not in the token: the session follows what the company
 * administrator gives or takes away through GET /users/me, and a 403 reads it again (docs/configurability.md → Giriş ve istek).
 */
describe('AuthService permissions', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  const user = (change: Partial<AuthUser> = {}): AuthUser => ({
    id: 7,
    fullName: 'Elif Yılmaz',
    email: 'elif.yilmaz@isdlc.local',
    permissions: ['Analysis'],
    isPlatformAdmin: false,
    tenant: { id: 1, code: 'ithinka', name: 'Ithinka' },
    isTenantAdmin: false,
    ...change,
  });

  const session = (change: Partial<AuthUser> = {}): LoginResponse => ({
    token: 'token',
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
    user: user(change),
  });

  function signIn(change: Partial<AuthUser> = {}): void {
    auth.login('elif.yilmaz@isdlc.local', 'parola').subscribe();
    http.expectOne(LOGIN_URL).flush(session(change));
  }

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([errorInterceptor])), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    vi.spyOn(window, 'alert').mockImplementation(() => undefined);
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('answers from the signed-in user\'s permissions', () => {
    signIn({ permissions: ['Analysis', 'Approval'] });

    expect(auth.hasPermission('Analysis')).toBe(true);
    expect(auth.hasPermission('Approval')).toBe(true);
    expect(auth.hasPermission('Testing')).toBe(false);
  });

  it('follows a permission the administrator gives or takes away, from GET /users/me', () => {
    signIn({ permissions: ['Analysis'] });

    auth.refreshMe();
    http.expectOne(ME_URL).flush(user({ permissions: ['Testing'] }));

    expect(auth.hasPermission('Analysis')).toBe(false);
    expect(auth.hasPermission('Testing')).toBe(true);
    // The token stays the same: only the user part of the session is replaced.
    expect(auth.token()).toBe('token');
  });

  it('keeps the session as it is when GET /users/me fails', () => {
    signIn({ permissions: ['Analysis'] });

    auth.refreshMe();
    http.expectOne(ME_URL).flush('Sunucu hatası', { status: 500, statusText: 'Server Error' });

    expect(auth.isLoggedIn()).toBe(true);
    expect(auth.hasPermission('Analysis')).toBe(true);
  });

  it('asks nothing while signed out', () => {
    auth.refreshMe();

    http.expectNone(ME_URL);
    expect(auth.hasPermission('Analysis')).toBe(false);
  });

  it('reads the user again after a 403 elsewhere, so the buttons follow', () => {
    signIn({ permissions: ['Analysis'] });

    TestBed.inject(HttpClient).post('/api/v1/projects/11/process/complete', {}).subscribe({ error: () => undefined });
    http.expectOne('/api/v1/projects/11/process/complete').flush(
      { title: 'Yetkiniz yok', status: 403, detail: 'Bu adım için «Analiz adımları» yetkisi gerekir.' },
      { status: 403, statusText: 'Forbidden' });
    http.expectOne(ME_URL).flush(user({ permissions: [] }));

    expect(auth.hasPermission('Analysis')).toBe(false);
  });

  it('does not loop when GET /users/me itself is refused', () => {
    signIn();

    auth.refreshMe();
    http.expectOne(ME_URL).flush({ status: 403 }, { status: 403, statusText: 'Forbidden' });

    http.expectNone(ME_URL);
  });

  it('sends the Platform Administrator to the company list and a temporary password to the change page', () => {
    expect(homeUrlFor(session({ isPlatformAdmin: true, tenant: null, permissions: [] }))).toBe('/platform/tenants');
    expect(homeUrlFor({ ...session(), mustChangePassword: true })).toBe('/change-password');
    expect(homeUrlFor(session({ permissions: [] }))).toBe('/dashboard');
    expect(homeUrlFor(null)).toBe('/dashboard');
  });
});
