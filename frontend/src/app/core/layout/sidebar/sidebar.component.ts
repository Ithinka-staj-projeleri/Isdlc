import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService, UserRole } from '../../auth/auth.service';

interface NavItem { path: string; label: string; roles?: UserRole[]; }
interface NavGroup { title: string; items: NavItem[]; }

const GROUPS: NavGroup[] = [
  {
    title: 'Çalışma', items: [
      { path: '/dashboard', label: 'Gösterge Paneli' },
      { path: '/projects', label: 'Projeler' },
    ]
  },
  {
    title: 'Analiz', items: [
      { path: '/assessment', label: 'Seviye & Kabul' },
      { path: '/requirements', label: 'Gereksinimler' },
      { path: '/risks', label: 'Risk Yönetimi' },
      { path: '/open-issues', label: 'Açık Konular' },
      { path: '/meetings', label: 'Toplantı Tutanakları' },
    ]
  },
];

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
})
export class SidebarComponent {
  private _auth = inject(AuthService);

  // roles tanımsız madde herkese görünür; tanımlıysa sadece o roller görür
  groups = computed(() =>
    GROUPS
      .map(g => ({
        ...g,
        items: g.items.filter(i => !i.roles || this._auth.hasRole(...i.roles)),
      }))
      .filter(g => g.items.length > 0),
  );
}