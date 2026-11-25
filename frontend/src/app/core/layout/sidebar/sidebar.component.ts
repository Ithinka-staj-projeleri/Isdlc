import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../auth/auth.service';

export interface NavItem { path: string; label: string; }
export interface NavGroup { title: string; items: NavItem[]; }

const WORK_GROUPS: NavGroup[] = [
  {
    title: 'Çalışma', items: [
      { path: '/dashboard', label: 'Gösterge Paneli' },
      { path: '/requests', label: 'Talepler' },
      { path: '/projects', label: 'Projeler' },
      { path: '/process-improvements', label: 'Süreç İyileştirme' },
    ]
  },
  {
    title: 'Analiz', items: [
      { path: '/assessment', label: 'Seviye & Uygunluk' },
      { path: '/requirements', label: 'Gereksinimler' },
      { path: '/risks', label: 'Risk Yönetimi' },
      { path: '/open-issues', label: 'Açık Konular' },
      { path: '/meetings', label: 'Toplantı Tutanakları' },
    ]
  },
  {
    title: 'Kütüphane', items: [
      { path: '/process-library', label: 'Süreç Kütüphanesi' },
    ]
  },
];

const TENANT_ADMIN_GROUP: NavGroup = {
  title: 'Yönetim', items: [
    { path: '/admin/users', label: 'Kullanıcılar' },
    { path: '/admin/settings', label: 'Şirket Ayarları' },
  ]
};

const PLATFORM_GROUP: NavGroup = {
  title: 'Platform', items: [
    { path: '/platform/tenants', label: 'Şirketler' },
  ]
};

/** The menu for who is signed in (docs/multi-tenancy.md → Ekranlar): the Platform Administrator sees only "Şirketler". */
export function navGroupsFor(user: { isPlatformAdmin: boolean; isTenantAdmin: boolean }): NavGroup[] {
  if (user.isPlatformAdmin) return [PLATFORM_GROUP];
  return user.isTenantAdmin ? [...WORK_GROUPS, TENANT_ADMIN_GROUP] : WORK_GROUPS;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
})
export class SidebarComponent {
  private readonly auth = inject(AuthService);

  protected readonly groups = computed(() => navGroupsFor({
    isPlatformAdmin: this.auth.isPlatformAdmin(),
    isTenantAdmin: this.auth.isTenantAdmin(),
  }));
}
