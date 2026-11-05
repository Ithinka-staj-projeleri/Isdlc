import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  templateUrl: './pagination.component.html',
  styleUrl: './pagination.component.scss',
})
export class PaginationComponent {
  page = input(1);
  pageSize = input(20);
  totalCount = input(0);
  pageChange = output<number>();

  totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));

  go(p: number) {
    if (p >= 1 && p <= this.totalPages()) this.pageChange.emit(p);
  }
}