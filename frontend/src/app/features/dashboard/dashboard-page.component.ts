import { Component, signal } from '@angular/core';
import { DataTableComponent, TableColumn } from '../../shared/data-table/data-table.component';
import { PaginatorComponent } from '../../shared/paginator/paginator.component';
import { StatusBadgeComponent } from '../../shared/status-badge/status-badge.component';

@Component({
  selector: 'app-dashboard-page',
  imports: [DataTableComponent, PaginatorComponent, StatusBadgeComponent],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
})
export class DashboardPageComponent {
  page = signal(1);
  columns: TableColumn[] = [
    { key: 'code', label: 'Kod' },
    { key: 'title', label: 'Başlık' },
    { key: 'status', label: 'Durum' },
  ];
  rows = [
    { code: 'ISDLC-2026-001', title: 'Satış raporu', status: 'Aktif' },
    { code: 'ISDLC-2026-002', title: 'Stok modülü', status: 'Beklemede' },
    { code: 'ISDLC-2026-003', title: 'Fatura entegrasyonu', status: 'Reddedildi' },
  ];
}