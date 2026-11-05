/** ProjectStatus as the API sends it (enum as text). */
export type ProjectStatus = 'Active' | 'Closed' | 'Rejected' | 'Lost' | 'Redirected';

/** Turkish status names shown on screen (same as the backend's ProjectStatusNames). */
export const PROJECT_STATUS_LABELS: Record<ProjectStatus, string> = {
    Active: 'Aktif',
    Closed: 'Kapandı',
    Rejected: 'Reddedildi',
    Lost: 'Kaybedildi',
    Redirected: 'Yönlendirildi',
};
