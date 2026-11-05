import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem { path: string; label: string; }
interface NavGroup { title: string; items: NavItem[]; }

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
})
export class SidebarComponent {
  groups: NavGroup[] = [
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
}