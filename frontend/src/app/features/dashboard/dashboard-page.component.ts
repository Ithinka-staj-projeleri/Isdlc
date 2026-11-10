import { Component, computed, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { DateOnlyPipe } from '../../shared/pipes/date-only.pipe';
import { LocalDatePipe } from '../../shared/pipes/local-date-pipe';
import { OPEN_ISSUE_TYPE_LABELS } from '../open-issues/models/open-issue';
import { STEP_ACTION_LABELS } from '../process/models/project-process';
import { DashboardService } from './dashboard.service';
import { DashboardScope } from './models/dashboard';

/** Stages 1–3 are requests ("Talepler"), 4–13 projects; the bars use a different color for each. */
const LAST_REQUEST_STAGE = 3;

/**
 * The dashboard: what waits for the signed-in user and how the projects stand. Contract: docs/dashboard-api.md
 * The scope lives in the URL (?scope=mine), so a refresh or the back button keeps it.
 */
@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, EmptyStateComponent, StatusBadgeComponent, DateOnlyPipe, LocalDatePipe],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss',
})
export class DashboardPageComponent {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _dashboardService = inject(DashboardService);
  private readonly _auth = inject(AuthService);

  protected readonly actionLabels = STEP_ACTION_LABELS;
  protected readonly issueTypeLabels = OPEN_ISSUE_TYPE_LABELS;
  protected readonly lastRequestStage = LAST_REQUEST_STAGE;

  protected readonly scope = toSignal(
    this._route.queryParamMap.pipe(
      map((params) => (params.get('scope') === 'mine' ? 'Mine' : 'All') as DashboardScope)),
    { initialValue: 'All' as DashboardScope },
  );

  protected readonly dashboard = rxResource({
    params: () => this.scope(),
    stream: ({ params }) => this._dashboardService.get(params),
  });

  /** "Elif" from "Elif Yılmaz". */
  protected readonly firstName = computed(() => this._auth.user()?.fullName.split(' ')[0] ?? '');

  /** The longest bar is full width; the others are scaled to it. */
  protected readonly maxStageCount = computed(() =>
    this.dashboard.hasValue() ? Math.max(1, ...this.dashboard.value().stages.map((s) => s.count)) : 1);

  protected readonly finishedTotal = computed(() => {
    if (!this.dashboard.hasValue()) {
      return 0;
    }
    const f = this.dashboard.value().summary.finishedThisYear;
    return f.closed + f.lost + f.rejected + f.redirected;
  });

  protected setScope(scope: DashboardScope): void {
    this._router.navigate([], {
      relativeTo: this._route,
      queryParams: { scope: scope === 'Mine' ? 'mine' : null },
      queryParamsHandling: 'merge',
    });
  }

  protected waitingText(days: number): string {
    return days === 0 ? 'Bugün' : `${days} gündür bekliyor`;
  }
}
