import { Component, input } from '@angular/core';

export type BadgeTone = 'ok' | 'warn' | 'bad' | 'info' | 'neutral';

@Component({
  selector: 'app-status-badge',
  templateUrl: './status-badge.component.html',
  styleUrl: './status-badge.component.scss',
})
export class StatusBadgeComponent {
  label = input.required<string>();
  tone = input<BadgeTone>('neutral');
}