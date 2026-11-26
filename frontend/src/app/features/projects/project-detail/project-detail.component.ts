import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { CopyLinkButtonComponent } from '../../../shared/components/copy-link-button/copy-link-button.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { projectLink, recordIdOf } from '../../../shared/models/record-link';
import { DateOnlyPipe } from '../../../shared/pipes/date-only.pipe';
import { LocalDatePipe } from '../../../shared/pipes/local-date-pipe';
import { ProjectAssessmentComponent } from '../../assessment/project-assessment/project-assessment.component';
import { AuditLogComponent } from '../../audit/audit-log/audit-log.component';
import { ProjectTimelineComponent } from '../../audit/project-timeline/project-timeline.component';
import { DocumentListComponent } from '../../documents/document-list/document-list.component';
import { MeetingListComponent } from '../../meetings/meeting-list/meeting-list.component';
import { OpenIssueListComponent } from '../../open-issues/open-issue-list/open-issue-list.component';
import { ProjectProcessComponent } from '../../process/project-process/project-process.component';
import { RequirementListComponent } from '../../requirements/requirement-list/requirement-list.component';
import { RiskListComponent } from '../../risks/risk-list/risk-list.component';
import { AddOnListComponent } from '../add-on-list/add-on-list.component';
import { ProjectSummaryComponent } from '../project-summary/project-summary.component';
import { RequestAnalysisTabComponent } from '../../request-analysis/request-analysis-tab/request-analysis-tab.component';
import {
  PROJECT_PRIORITY_LABELS,
  PROJECT_STATUS_LABELS,
  PROJECT_STATUS_TONES,
  REQUEST_CHANNEL_LABELS,
  REQUEST_SOURCE_LABELS,
  REQUEST_TYPE_LABELS,
} from '../project-labels';
import { ProjectService } from '../project.service';
import { formatCustomValue } from '../custom-fields/custom-field';
import { CustomFieldService } from '../custom-fields/custom-field.service';

type DetailTab =
  | 'general'
  | 'process'
  | 'timeline'
  | 'request-analysis'
  | 'add-ons'
  | 'assessment'
  | 'requirements'
  | 'documents'
  | 'risks'
  | 'open-issues'
  | 'meetings'
  | 'history';

/** In the new software project flow stages 1–2 are the request phase ("Talepler"); 3–4 the project phase ("Projeler"). */
const LAST_REQUEST_STAGE = 2;

/**
 * Project detail: a summary on top and one tab per module.
 * Each tab is its owner's component and only needs the project id (docs/projects-api.md → Ekranlar).
 * The chosen tab is kept in the URL (?tab=process).
 */
@Component({
  selector: 'app-project-detail',
  imports: [
    RouterLink,
    CopyLinkButtonComponent,
    DateOnlyPipe,
    LocalDatePipe,
    EmptyStateComponent,
    StatusBadgeComponent,
    ProjectProcessComponent,
    OpenIssueListComponent,
    MeetingListComponent,
    RequirementListComponent,
    RiskListComponent,
    ProjectAssessmentComponent,
    DocumentListComponent,
    AuditLogComponent,
    ProjectTimelineComponent,
    AddOnListComponent,
    ProjectSummaryComponent,
    RequestAnalysisTabComponent,
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
})
export class ProjectDetailComponent {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _projectService = inject(ProjectService);

  readonly statusLabels = PROJECT_STATUS_LABELS;
  readonly statusTones = PROJECT_STATUS_TONES;
  readonly priorityLabels = PROJECT_PRIORITY_LABELS;
  readonly typeLabels = REQUEST_TYPE_LABELS;
  readonly sourceLabels = REQUEST_SOURCE_LABELS;
  readonly channelLabels = REQUEST_CHANNEL_LABELS;

  private static readonly ALL_TABS: { key: DetailTab; label: string }[] = [
    { key: 'general', label: 'Genel Bilgiler' },
    { key: 'process', label: 'Süreç' },
    { key: 'timeline', label: 'Zaman Çizelgesi' },
    { key: 'request-analysis', label: 'Talep Analizi' },
    { key: 'add-ons', label: 'Ek Geliştirmeler' },
    { key: 'assessment', label: 'Seviye & Uygunluk' },
    { key: 'requirements', label: 'Gereksinimler' },
    { key: 'documents', label: 'Dokümanlar' },
    { key: 'risks', label: 'Riskler' },
    { key: 'open-issues', label: 'Açık Konular' },
    { key: 'meetings', label: 'Toplantı Tutanakları' },
    { key: 'history', label: 'Kayıt Geçmişi' },
  ];

  /** An add-on has no add-ons of its own, so it has no "Ek Geliştirmeler" tab. */
  readonly tabs = computed(() =>
    ProjectDetailComponent.ALL_TABS.filter((t) => t.key !== 'add-ons' || !this.loaded()?.parentProject),
  );

  readonly projectId = toSignal(this._route.paramMap.pipe(map((params) => Number(params.get('projectId')))), {
    requireSync: true,
  });

  readonly tab = toSignal(
    this._route.queryParamMap.pipe(map((params) => (params.get('tab') as DetailTab | null) ?? 'general')),
    { requireSync: true },
  );

  /** The record the tab opens: ?id=12 (Faz 5 · F8, docs/record-links.md). */
  readonly recordId = toSignal(this._route.queryParamMap.pipe(map((params) => recordIdOf(params.get('id')))), {
    requireSync: true,
  });

  readonly link = computed(() => projectLink(this.projectId()));

  /** The company's active fields, for "Şirkete Özel Bilgiler" (Faz 7 · A6). */
  private readonly _customFieldService = inject(CustomFieldService);
  private readonly _customFields = rxResource({ stream: () => this._customFieldService.list() });
  readonly customFields = computed(() => (this._customFields.hasValue() ? this._customFields.value() : []));
  readonly formatCustomValue = formatCustomValue;

  readonly project = rxResource({
    params: () => ({ projectId: this.projectId() }),
    stream: ({ params }) => this._projectService.get(params.projectId),
  });

  /** The project when loaded. Reading a resource's value in the error state throws, so it is checked first. */
  readonly loaded = computed(() => (this.project.hasValue() ? this.project.value() : undefined));

  readonly notFound = computed(() => {
    const error = this.project.error();
    const cause = (error as { cause?: unknown } | undefined)?.cause ?? error;
    return cause instanceof HttpErrorResponse && cause.status === 404;
  });

  /** Back to the list the project belongs to; an add-on goes back to its project's "Ek Geliştirmeler" tab. */
  readonly backLink = computed(() => {
    const parent = this.loaded()?.parentProject;
    if (parent) {
      return { path: `/projects/${parent.id}`, label: `${parent.code} · ${parent.name}`, query: { tab: 'add-ons' } };
    }
    const stageOrder = this.loaded()?.currentStep?.stageOrder;
    return stageOrder !== undefined && stageOrder <= LAST_REQUEST_STAGE
      ? { path: '/requests', label: 'Talepler', query: {} }
      : { path: '/projects', label: 'Projeler', query: {} };
  });

  /**
   * Whether an add-on can be opened on this project now: a new software project that has been accepted (past the
   * request stages) or is closed. The API checks the same rule.
   */
  readonly acceptsAddOns = computed(() => {
    const p = this.loaded();
    if (!p || p.parentProject) {
      return false;
    }
    return p.status === 'Closed' || (p.status === 'Active' && (p.currentStep?.stageOrder ?? 0) > LAST_REQUEST_STAGE);
  });

  /** Open issues and meetings can only be changed while the project is active. */
  readonly readOnly = computed(() => this.loaded()?.status !== 'Active');

  selectTab(tab: DetailTab): void {
    this._router.navigate([], {
      relativeTo: this._route,
      queryParams: { tab: tab === 'general' ? null : tab, id: null },
      queryParamsHandling: 'merge',
    });
  }

  /** The tab closed the record it opened from the address, so a reload does not open it again. */
  clearRecordId(): void {
    if (this.recordId() === null) {
      return;
    }
    this._router.navigate([], {
      relativeTo: this._route,
      queryParams: { id: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
