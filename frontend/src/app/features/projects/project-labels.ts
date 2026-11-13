import { BadgeTone } from '../../shared/components/status-badge/status-badge.component';
import { ProjectStatus } from '../../shared/models/project-status';
import { ProjectPriority, RequestChannel, RequestSource, RequestType } from './project.model';

// Turkish text for the backend's enum values (AGENTS.md: everything on screen is Turkish).

/** Status names are shared with other modules, so they live in shared/models/project-status. */
export { PROJECT_STATUS_LABELS } from '../../shared/models/project-status';

/** Badge colour per status: running blue, finished green, stopped red, sent elsewhere yellow. */
export const PROJECT_STATUS_TONES: Record<ProjectStatus, BadgeTone> = {
  Active: 'info',
  Closed: 'ok',
  Rejected: 'bad',
  Lost: 'bad',
  Redirected: 'warn',
  Postponed: 'warn',
};

export const PROJECT_PRIORITY_LABELS: Record<ProjectPriority, string> = {
  Low: 'Düşük',
  Medium: 'Orta',
  High: 'Yüksek',
};

export const REQUEST_TYPE_LABELS: Record<RequestType, string> = {
  NewProject: 'Yeni yazılım projesi',
  AdditionalModule: 'Ek geliştirme',
  CompanyProduct: 'Şirket ürünü',
  Support: 'Destek',
};

/**
 * The source options of the company's Talep Kayıt Formu, in its words. İlişkili şirket is not on the form
 * (the question bank uses it), so the request form offers only these two.
 */
export const REQUEST_FORM_SOURCE_OPTIONS: [RequestSource, string][] = [
  ['Internal', 'Kurum İçi Talepler'],
  ['External', 'Dış Talepler (Müşteri)'],
];

export const REQUEST_SOURCE_LABELS: Record<RequestSource, string> = {
  Internal: 'Kurum içi',
  External: 'Dış (müşteri)',
  Affiliated: 'İlişkili şirket',
};

export const REQUEST_CHANNEL_LABELS: Record<RequestChannel, string> = {
  Email: 'E-posta',
  Phone: 'Telefon',
  Meeting: 'Toplantı',
  Other: 'Diğer',
};
