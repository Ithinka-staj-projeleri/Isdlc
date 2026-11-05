import { Component, input, output } from '@angular/core';
import { EmptyStateComponent } from '../empty-state/empty-state.component';

export interface TableColumn { key: string; label: string; }

@Component({
  selector: 'app-data-table',
  imports: [EmptyStateComponent],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
})
export class DataTableComponent {
  columns = input.required<TableColumn[]>();
  rows = input<Record<string, unknown>[]>([]);
  rowClick = output<Record<string, unknown>>();
}