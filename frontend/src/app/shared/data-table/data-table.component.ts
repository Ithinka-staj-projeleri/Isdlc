import { NgTemplateOutlet } from '@angular/common';
import { Component, input, output, TemplateRef } from '@angular/core';

export interface TableColumn {
  key: string;
  label: string;
}

export type TableRow = Record<string, unknown>;

@Component({
  selector: 'app-data-table',
  imports: [NgTemplateOutlet],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.scss',
})
export class DataTableComponent {
  columns = input.required<TableColumn[]>();
  rows = input<TableRow[]>([]);
  cellTemplate = input<TemplateRef<unknown> | null>(null);
  rowClick = output<TableRow>();
}