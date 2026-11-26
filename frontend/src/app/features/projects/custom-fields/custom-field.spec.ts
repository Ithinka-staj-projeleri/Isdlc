import { formatCustomValue, keyFromLabel, toCustomValue } from './custom-field';

describe('custom fields (Faz 7 · A6)', () => {
  it('suggests a key from a Turkish label', () => {
    expect(keyFromLabel('Bütçe onaylı mı?')).toBe('butce-onayli-mi');
    expect(keyFromLabel('  Sözleşme No ')).toBe('sozlesme-no');
    expect(keyFromLabel('İlçe / Şube')).toBe('ilce-sube');
  });

  it('shows saved values as the screens read them', () => {
    expect(formatCustomValue({ type: 'Boolean' }, true)).toBe('Evet');
    expect(formatCustomValue({ type: 'Boolean' }, false)).toBe('Hayır');
    expect(formatCustomValue({ type: 'Date' }, '2026-12-01')).toBe('01.12.2026');
    expect(formatCustomValue({ type: 'Text' }, undefined)).toBe('—');
  });

  it('sends what the API expects and leaves out empty values', () => {
    expect(toCustomValue({ type: 'Text' }, '  S-14 ')).toBe('S-14');
    expect(toCustomValue({ type: 'Text' }, '   ')).toBeNull();
    expect(toCustomValue({ type: 'Number' }, '12,5')).toBe(12.5);
    expect(toCustomValue({ type: 'Number' }, null)).toBeNull();
    expect(toCustomValue({ type: 'Boolean' }, false)).toBe(false);
    expect(toCustomValue({ type: 'Select' }, null)).toBeNull();
  });
});
