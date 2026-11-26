import { homeUrlFor } from '../../../core/auth/auth.service';
import { navGroupsFor } from '../../../core/layout/sidebar/sidebar.component';
import { checkNewPassword, generateTemporaryPassword } from '../../identity/models/password';
import { isValidTenantCode, suggestTenantCode } from './tenant';

// Faz 6 · T5 rules (docs/multi-tenancy.md → Ekranlar, Parola, Hata mesajları).

describe('tenant code', () => {
  it('follows the backend rule: lower-case letters, digits and single hyphens, 2–50 characters', () => {
    expect(isValidTenantCode('ornek-lojistik')).toBe(true);
    expect(isValidTenantCode('a1')).toBe(true);
    expect(isValidTenantCode('a')).toBe(false);
    expect(isValidTenantCode('Ornek')).toBe(false);
    expect(isValidTenantCode('ornek--lojistik')).toBe(false);
    expect(isValidTenantCode('-ornek')).toBe(false);
    expect(isValidTenantCode('ornek lojistik')).toBe(false);
    expect(isValidTenantCode('a'.repeat(51))).toBe(false);
  });

  it('suggests a valid code from a Turkish company name', () => {
    expect(suggestTenantCode('Örnek Lojistik A.Ş.')).toBe('ornek-lojistik-a-s');
    expect(suggestTenantCode('İstanbul Çağrı Ğüşı')).toBe('istanbul-cagri-gusi');
    expect(suggestTenantCode('  Deneme  ')).toBe('deneme');
    expect(isValidTenantCode(suggestTenantCode('Örnek Lojistik A.Ş.'))).toBe(true);
    expect(suggestTenantCode('x'.repeat(49) + ' yz').length).toBeLessThanOrEqual(50);
    expect(suggestTenantCode('x'.repeat(49) + ' yz').endsWith('-')).toBe(false);
  });
});

describe('passwords', () => {
  it('checks the new password like the API', () => {
    expect(checkNewPassword('', 'yeni-parola-123', 'yeni-parola-123')).toBe('Mevcut ve yeni parola zorunludur.');
    expect(checkNewPassword('gecici-parola-1', 'kisa', 'kisa')).toBe('Parola en az 12 karakter olmalıdır.');
    expect(checkNewPassword('ayni-parola-123', 'ayni-parola-123', 'ayni-parola-123')).toBe('Yeni parola eskisinden farklı olmalıdır.');
    expect(checkNewPassword('gecici-parola-1', 'yeni-parola-123', 'yeni-parola-124')).toBe('Yeni parolalar aynı değil.');
    expect(checkNewPassword('gecici-parola-1', 'yeni-parola-123', 'yeni-parola-123')).toBeNull();
  });

  it('generates temporary passwords long enough and without look-alike characters', () => {
    const password = generateTemporaryPassword();
    expect(password.length).toBe(16);
    expect(password).toMatch(/^[A-HJ-NP-Za-km-np-z2-9]+$/);
    expect(generateTemporaryPassword()).not.toBe(password);
  });
});

describe('who sees what', () => {
  const user = (kind: 'BusinessAnalyst' | 'PlatformAdmin') => ({
    id: 1, fullName: 'X', email: 'x@x', tenant: null, isTenantAdmin: false,
    isPlatformAdmin: kind === 'PlatformAdmin',
    permissions: kind === 'PlatformAdmin' ? [] : ['Analysis' as const],
  });

  it('sends a temporary password to the password change, the Platform Administrator to the companies', () => {
    expect(homeUrlFor({ user: user('BusinessAnalyst'), mustChangePassword: true })).toBe('/change-password');
    expect(homeUrlFor({ user: user('PlatformAdmin'), mustChangePassword: true })).toBe('/change-password');
    expect(homeUrlFor({ user: user('PlatformAdmin') })).toBe('/platform/tenants');
    expect(homeUrlFor({ user: user('BusinessAnalyst'), mustChangePassword: false })).toBe('/dashboard');
    expect(homeUrlFor(null)).toBe('/dashboard');
  });

  it('shows the Platform Administrator only the companies, and the company administrator the admin pages', () => {
    const paths = (groups: { items: { path: string }[] }[]) => groups.flatMap(g => g.items.map(i => i.path));

    expect(paths(navGroupsFor({ isPlatformAdmin: true, isTenantAdmin: false }))).toEqual(['/platform/tenants']);

    const admin = paths(navGroupsFor({ isPlatformAdmin: false, isTenantAdmin: true }));
    expect(admin).toContain('/admin/users');
    expect(admin).toContain('/admin/settings');
    expect(admin).not.toContain('/platform/tenants');

    const member = paths(navGroupsFor({ isPlatformAdmin: false, isTenantAdmin: false }));
    expect(member).toContain('/dashboard');
    expect(member.some(p => p.startsWith('/admin') || p.startsWith('/platform'))).toBe(false);
  });
});
