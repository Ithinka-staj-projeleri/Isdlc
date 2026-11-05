import { BadgeTone } from '../../shared/components/status-badge/status-badge.component';
import { ProjectStatus, RequestChannel, RequestSource, RequestType } from './project.model';

// Turkish text for the backend's enum values (AGENTS.md: everything on screen is Turkish).

export const PROJECT_STATUS_LABELS: Record<ProjectStatus, string> = {
  Active: 'Aktif',
  Closed: 'Kapandı',
  Rejected: 'Reddedildi',
  Lost: 'Kaybedildi',
  Redirected: 'Yönlendirildi',
};

/** Badge colour per status: running blue, finished green, stopped red, sent elsewhere yellow. */
export const PROJECT_STATUS_TONES: Record<ProjectStatus, BadgeTone> = {
  Active: 'info',
  Closed: 'ok',
  Rejected: 'bad',
  Lost: 'bad',
  Redirected: 'warn',
};

export const REQUEST_TYPE_LABELS: Record<RequestType, string> = {
  NewProject: 'Yeni yazılım projesi',
  AdditionalModule: 'Ek modül',
  CompanyProduct: 'Şirket ürünü',
  Support: 'Destek',
};

export const REQUEST_SOURCE_LABELS: Record<RequestSource, string> = {
  Internal: 'İç',
  External: 'Dış',
};

export const REQUEST_CHANNEL_LABELS: Record<RequestChannel, string> = {
  Written: 'Yazılı',
  Verbal: 'Sözlü',
};
