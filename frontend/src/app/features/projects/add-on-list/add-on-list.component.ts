import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { PROJECT_STATUS_LABELS, PROJECT_STATUS_TONES } from '../project-labels';
import { ProjectService } from '../project.service';

/**
 * "Ek Geliştirmeler" tab of a project: its add-ons (ek geliştirme), newest first, and a button that opens the
 * request form for a new add-on of this project. Contract: docs/projects-api.md → "Ek geliştirme" (D3).
 */
@Component({
  selector: 'app-add-on-list',
  imports: [RouterLink, EmptyStateComponent, StatusBadgeComponent],
  templateUrl: './add-on-list.component.html',
  styleUrl: './add-on-list.component.scss',
})
export class AddOnListComponent {
  private readonly _projectService = inject(ProjectService);
  private readonly _router = inject(Router);
  private readonly _auth = inject(AuthService);

  readonly projectId = input.required<number>();

  /** Whether the project can get add-ons now (accepted or closed); the API checks the same rule. */
  readonly acceptsAddOns = input.required<boolean>();

  readonly statusLabels = PROJECT_STATUS_LABELS;
  readonly statusTones = PROJECT_STATUS_TONES;

  readonly addOns = rxResource({
    params: () => this.projectId(),
    stream: ({ params }) =>
      this._projectService.list({ parentProjectId: params, pageSize: 100 }).pipe(map((page) => page.items)),
  });

  readonly items = computed(() => (this.addOns.hasValue() ? this.addOns.value() : []));

  /** Only a Business Analyst opens requests, add-ons included. */
  readonly canAdd = computed(() => this.acceptsAddOns() && this._auth.user()?.role === 'BusinessAnalyst');

  open(id: number): void {
    this._router.navigate(['/projects', id]);
  }
}
