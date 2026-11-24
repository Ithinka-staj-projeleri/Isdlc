import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { USER_ROLE_LABELS } from '../../../shared/models/user-role';
import { TeamMemberWorkload } from '../models/dashboard';

/**
 * Ekip iş yükü (Faz 5 · F2): every active user's open issues and waiting steps on active records, the busiest first.
 * The bar shows open issues and waiting steps together, relative to the busiest person.
 * Contract: docs/dashboard-api.md → "Ekip iş yükü".
 */
@Component({
  selector: 'app-team-workload',
  imports: [RouterLink],
  templateUrl: './team-workload.component.html',
  styleUrl: './team-workload.component.scss',
})
export class TeamWorkloadComponent {
  readonly members = input.required<TeamMemberWorkload[]>();

  protected readonly roleLabels = USER_ROLE_LABELS;

  /** The busiest person fills the bar. */
  protected readonly max = computed(() =>
    Math.max(1, ...this.members().map(m => m.openIssues + m.waitingSteps)));

  protected percent(value: number): number {
    return (value / this.max()) * 100;
  }
}
