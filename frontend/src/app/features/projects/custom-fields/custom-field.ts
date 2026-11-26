// Shapes of the custom fields API (Faz 7 · A6). Contract: docs/configurability.md → Şirkete özel alanlar.

export type CustomFieldType = 'Text' | 'Number' | 'Date' | 'Select' | 'Boolean';

/** A value on a record: text, number, date ("yyyy-MM-dd"), chosen option or yes / no. */
export type CustomValue = string | number | boolean;

/** A field the company adds to its request form. */
export interface CustomField {
  id: number;
  /** The name of the value on the record and in skip rules ("custom:{key}"). Never changes. */
  key: string;
  label: string;
  /** Never changes. */
  type: CustomFieldType;
  /** Only for Select. */
  options: string[];
  isRequired: boolean;
  helpText: string | null;
  order: number;
  isActive: boolean;
}

/** What "Yönetim → Talep alanları" sends. Key and type are sent only when the field is added. */
export interface CustomFieldInput {
  label: string;
  options: string[];
  isRequired: boolean;
  helpText: string | null;
  order: number | null;
}

export interface CreateCustomFieldInput extends CustomFieldInput {
  key: string;
  type: CustomFieldType;
}

export interface UpdateCustomFieldInput extends CustomFieldInput {
  isActive: boolean;
}

export const CUSTOM_FIELD_TYPE_LABELS: Record<CustomFieldType, string> = {
  Text: 'Metin',
  Number: 'Sayı',
  Date: 'Tarih',
  Select: 'Seçim',
  Boolean: 'Evet / Hayır',
};

/** Small letters, digits and dashes, 2–50 (same as the API). */
export const CUSTOM_FIELD_KEY_PATTERN = /^[a-z0-9-]{2,50}$/;

/**
 * A key suggested from the label: Turkish letters to their plain forms, everything else to dashes.
 * "Bütçe onaylı mı?" → "butce-onayli-mi".
 */
export function keyFromLabel(label: string): string {
  const plain: Record<string, string> = { ç: 'c', ğ: 'g', ı: 'i', ö: 'o', ş: 's', ü: 'u', â: 'a', î: 'i', û: 'u' };
  return label
    .toLocaleLowerCase('tr-TR')
    .replace(/[çğıöşüâîû]/g, (c) => plain[c] ?? c)
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 50);
}

/** A saved value as the screens show it; empty values are "—". */
export function formatCustomValue(field: Pick<CustomField, 'type'>, value: CustomValue | null | undefined): string {
  if (value === null || value === undefined || value === '') {
    return '—';
  }
  switch (field.type) {
    case 'Boolean':
      return value === true ? 'Evet' : value === false ? 'Hayır' : String(value);
    case 'Date': {
      const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value));
      return match ? `${match[3]}.${match[2]}.${match[1]}` : String(value);
    }
    case 'Number':
      return typeof value === 'number' ? value.toLocaleString('tr-TR') : String(value);
    default:
      return String(value);
  }
}

/**
 * A form control's value as the API wants it: blank text and an unchosen option are "not given" (left out),
 * a number box gives a number, yes / no gives true / false.
 */
export function toCustomValue(field: Pick<CustomField, 'type'>, raw: unknown): CustomValue | null {
  if (raw === null || raw === undefined) {
    return null;
  }
  switch (field.type) {
    case 'Boolean':
      return raw === true || raw === 'true' ? true : raw === false || raw === 'false' ? false : null;
    case 'Number': {
      if (typeof raw === 'number') {
        return Number.isFinite(raw) ? raw : null;
      }
      const text = String(raw).trim().replace(',', '.');
      return text === '' || Number.isNaN(Number(text)) ? null : Number(text);
    }
    default: {
      const text = String(raw).trim();
      return text === '' ? null : text;
    }
  }
}
