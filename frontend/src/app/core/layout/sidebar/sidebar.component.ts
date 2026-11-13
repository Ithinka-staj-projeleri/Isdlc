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
        { path: '/requests', label: 'Talepler' },
        { path: '/projects', label: 'Projeler' },
        { path: '/process-improvements', label: 'Süreç İyileştirme' },
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
    {
      title: 'Kütüphane', items: [
        { path: '/process-library', label: 'Süreç Kütüphanesi' },
      ]
    },
  ];
}