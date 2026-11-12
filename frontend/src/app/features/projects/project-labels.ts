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
};

export const PROJECT_PRIORITY_LABELS: Record<ProjectPriority, string> = {
  Low: 'Düşük',
  Medium: 'Orta',
  High: 'Yüksek',
};

export const REQUEST_TYPE_LABELS: Record<RequestType, string> = {
  NewProject: 'Yeni yazılım projesi',
  AdditionalModule: 'Ek modül',
  CompanyProduct: 'Şirket ürünü',
  Support: 'Destek',
};

export const REQUEST_SOURCE_LABELS: Record<RequestSource, string> = {
  Internal: 'Kurum içi',
  External: 'Dış (müşteri)',
  Affiliated: 'İlişkili şirket',
};

export const REQUEST_CHANNEL_LABELS: Record<RequestChannel, string> = {
  Email: 'E-posta',
  Phone: 'Telefon',
};
