import { askAdmins, matchingPreset, newRequestBlockedMessage, permissionNames, splitPermissionName } from './permission';

describe('permissions on screen', () => {
  it('lists names in the fixed order, or says there are none', () => {
    expect(permissionNames(['Testing', 'Analysis'])).toBe('Analiz adımları, Test');
    expect(permissionNames([])).toBe('Yetki yok');
    expect(permissionNames(undefined, 'Yetkisiz')).toBe('Yetkisiz');
  });

  it('finds the ready-made set that matches the boxes exactly', () => {
    const presets = [
      { name: 'İş Analisti', permissions: ['Analysis' as const] },
      { name: 'Test Uzmanı', permissions: ['Testing' as const] },
    ];
    expect(matchingPreset(presets, ['Analysis'])?.name).toBe('İş Analisti');
    expect(matchingPreset(presets, ['Analysis', 'Testing'])).toBeNull();
    expect(matchingPreset(presets, [])).toBeNull();
  });

  it('says whom to ask, like the API', () => {
    expect(askAdmins(['Deniz Aydın', 'Mert Kaya'])).toBe('Şirket yöneticinizden isteyin: Deniz Aydın, Mert Kaya.');
    expect(askAdmins([])).toBe('Şirket yöneticinizden isteyin.');
    expect(newRequestBlockedMessage(['Mert Kaya']))
      .toBe('Talep açmak için «Analiz adımları» yetkisi gerekir. Şirket yöneticinizden isteyin: Mert Kaya.');
  });

  it('splits a refusal around the permission name', () => {
    expect(splitPermissionName('Bu adım için «Test» yetkisi gerekir.'))
      .toEqual({ before: 'Bu adım için ', name: 'Test', after: ' yetkisi gerekir.' });
    expect(splitPermissionName('Proje aktif değil.')).toEqual({ before: 'Proje aktif değil.', name: null, after: '' });
  });
});
