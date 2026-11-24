import { Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { RecordLink } from '../../models/record-link';

/** How long "Kopyalandı" stays on the button. */
const FEEDBACK_MS = 2000;

/**
 * "Bağlantıyı kopyala" (Faz 5 · F8): copies the full address of a record, so whoever opens it sees the same
 * tab and record. Contract: docs/record-links.md
 */
@Component({
  selector: 'app-copy-link-button',
  template: `
    <button type="button" class="copy-link" [title]="url()" (click)="copy()">
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7-7l-1.7 1.7" />
        <path d="M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7 7l1.7-1.7" />
      </svg>
      <span aria-live="polite">{{ label() }}</span>
    </button>
  `,
  styleUrl: './copy-link-button.component.scss',
})
export class CopyLinkButtonComponent {
  private readonly router = inject(Router);

  readonly link = input.required<RecordLink>();

  private readonly state = signal<'idle' | 'copied' | 'failed'>('idle');
  private timer: ReturnType<typeof setTimeout> | undefined;

  /** The full address, with the app's own origin and base href. */
  protected readonly url = computed(() => {
    const { commands, queryParams } = this.link();
    const path = this.router.serializeUrl(this.router.createUrlTree(commands, { queryParams }));
    return new URL(path.replace(/^\//, ''), document.baseURI).href;
  });

  protected readonly label = computed(() => {
    switch (this.state()) {
      case 'copied': return 'Kopyalandı';
      case 'failed': return 'Kopyalanamadı';
      default: return 'Bağlantıyı kopyala';
    }
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.url());
      this.show('copied');
    } catch {
      // The clipboard needs a secure page and the user's permission; the address stays in the title.
      this.show('failed');
    }
  }

  private show(state: 'copied' | 'failed'): void {
    this.state.set(state);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.state.set('idle'), FEEDBACK_MS);
  }
}
