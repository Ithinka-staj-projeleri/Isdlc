import { Component, DestroyRef, ElementRef, HostListener, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, filter, map, of, switchMap } from 'rxjs';
import { SEARCH_MAX_LENGTH, SearchRow, SearchRowGroup, searchText, toRowGroups } from '../models/search';
import { SearchService } from '../search.service';

/** Wait after the last key before asking (docs/search-api.md → Ekran). */
const DEBOUNCE_MS = 300;

type PanelState = 'idle' | 'loading' | 'error' | 'done';

/**
 * The search box in the top bar (Faz 5 · F7). Ctrl+K (Cmd+K) focuses it; results open in a panel below,
 * grouped by kind. Arrow keys move, Enter opens, Esc closes. Contract: docs/search-api.md
 */
@Component({
  selector: 'app-global-search',
  templateUrl: './global-search.component.html',
  styleUrl: './global-search.component.scss',
})
export class GlobalSearchComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly destroyRef = inject(DestroyRef);
  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('input');

  private readonly typed = new Subject<string>();

  protected readonly maxLength = SEARCH_MAX_LENGTH;
  protected readonly text = signal('');
  protected readonly open = signal(false);
  protected readonly state = signal<PanelState>('idle');
  protected readonly groups = signal<SearchRowGroup[]>([]);
  protected readonly activeIndex = signal(-1);

  /** Every row in panel order, for the keyboard. */
  protected readonly rows = computed(() => this.groups().flatMap(g => g.rows));
  protected readonly activeKey = computed(() => this.rows()[this.activeIndex()]?.key ?? null);

  protected readonly shortcut = /Mac|iPhone|iPad/.test(navigator.platform) ? '⌘K' : 'Ctrl+K';

  ngOnInit(): void {
    this.typed
      .pipe(
        map(searchText),
        debounceTime(DEBOUNCE_MS),
        distinctUntilChanged(),
        // switchMap drops the answer of an older request when a newer one starts.
        switchMap(text => {
          if (text === null) {
            return of(null);
          }

          this.state.set('loading');
          return this.searchService.search(text).pipe(
            map(result => toRowGroups(result)),
            catchError(() => of('error' as const)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.activeIndex.set(-1);
        if (result === null) {
          this.groups.set([]);
          this.state.set('idle');
        } else if (result === 'error') {
          this.groups.set([]);
          this.state.set('error');
        } else {
          this.groups.set(result);
          this.state.set('done');
          this.activeIndex.set(result.length > 0 ? 0 : -1);
        }
      });

    this.router.events
      .pipe(filter(e => e instanceof NavigationEnd), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.open.set(false));
  }

  protected onInput(value: string): void {
    this.text.set(value);
    this.open.set(true);
    this.typed.next(value);
  }

  protected onFocus(): void {
    if (searchText(this.text()) !== null) {
      this.open.set(true);
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    const count = this.rows().length;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.open.set(true);
        if (count > 0) this.activeIndex.update(i => (i + 1) % count);
        break;
      case 'ArrowUp':
        event.preventDefault();
        if (count > 0) this.activeIndex.update(i => (i <= 0 ? count - 1 : i - 1));
        break;
      case 'Enter': {
        const row = this.rows()[this.activeIndex()];
        if (this.open() && row) {
          event.preventDefault();
          this.go(row);
        }
        break;
      }
      case 'Escape':
        this.close();
        this.input().nativeElement.blur();
        break;
    }
  }

  protected go(row: SearchRow): void {
    this.close();
    this.router.navigate(row.link.commands, { queryParams: row.link.queryParams });
  }

  protected setActive(row: SearchRow): void {
    this.activeIndex.set(this.rows().indexOf(row));
  }

  private close(): void {
    this.open.set(false);
  }

  /** Ctrl+K (Cmd+K on a Mac) from anywhere focuses the box, instead of the browser's own search. */
  @HostListener('document:keydown', ['$event'])
  protected onDocumentKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      const input = this.input().nativeElement;
      input.focus();
      input.select();
      this.onFocus();
    }
  }

  /** A click outside closes the panel. */
  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }
}
