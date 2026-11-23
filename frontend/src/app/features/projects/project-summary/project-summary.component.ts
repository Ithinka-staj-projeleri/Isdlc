import { Component, computed, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { OPEN_ISSUE_TYPE_LABELS } from '../../open-issues/models/open-issue';
import { ProjectService } from '../project.service';

/** The project detail tabs the overview links to. */
export type SummaryTab = 'open-issues' | 'risks' | 'requirements' | 'documents';

/**
 * Proje özeti (Faz 5 · F1): on top of "Genel Bilgiler", how the project's open issues, risks, requirements and documents
 * stand and who carries the open issues. Each box opens its tab. Contract: docs/projects-api.md → "Proje özeti".
 */
@Component({
  selector: 'app-project-summary',
  templateUrl: './project-summary.component.html',
  styleUrl: './project-summary.component.scss',
})
export class ProjectSummaryComponent {
  private readonly _projectService = inject(ProjectService);

  readonly projectId = input.required<number>();

  /** A box was clicked: the detail page opens that tab. */
  readonly openTab = output<SummaryTab>();

  protected readonly typeLabels = OPEN_ISSUE_TYPE_LABELS;

  protected readonly summary = rxResource({
    params: () => this.projectId(),
    stream: ({ params }) => this._projectService.summary(params),
  });

  // A resource throws when its value is read in the error state; read it only when there is a value.
  protected readonly s = computed(() => (this.summary.hasValue() ? this.summary.value() : undefined));

  /** Open and in progress: everything still waiting. */
  protected readonly waitingIssues = computed(() => {
    const s = this.s();
    return s ? s.openIssues.open + s.openIssues.inProgress : 0;
  });

  protected readonly followedRisks = computed(() => {
    const r = this.s()?.risks;
    return r ? r.high + r.medium + r.low : 0;
  });

  /** Cancelled requirements are not part of the work. */
  protected readonly activeRequirements = computed(() => {
    const r = this.s()?.requirements;
    return r ? r.draft + r.inReview + r.approved : 0;
  });

  protected readonly documentPercent = computed(() => {
    const d = this.s()?.documents;
    return d && d.tracked > 0 ? Math.round((d.approved / d.tracked) * 100) : 0;
  });

  /** The busiest owner fills the bar; the others are relative to them. */
  protected readonly maxWorkload = computed(() => Math.max(1, ...(this.s()?.workload.map(w => w.open) ?? [])));
}
