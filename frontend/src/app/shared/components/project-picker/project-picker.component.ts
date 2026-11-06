import { Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { PROJECT_STATUS_LABELS } from '../../models/project-status';
import { LookupService, ProjectOption } from '../../services/lookup.service';

/**
 * Project selector for menu pages that show one project's records (Açık Konular, Toplantı Tutanakları).
 * The choice is kept in the URL (?projectId=3); without one, the first active project is chosen.
 * Read the choice with the selected() signal.
 */
@Component({
    selector: 'app-project-picker',
    imports: [FormsModule],
    template: `
        <label class="picker">
            Proje
            <select [ngModel]="selected()?.id ?? null" (ngModelChange)="choose($event)">
                @if (!selected()) { <option [ngValue]="null">Proje seçin…</option> }
                @for (project of projects(); track project.id) {
                <option [ngValue]="project.id">
                    {{ project.code }} · {{ project.name }}{{ project.status === 'Active' ? '' : ' (' + statusLabels[project.status] + ')' }}
                </option>
                }
            </select>
        </label>
    `,
    styles: `
        @use '../../styles/ui' as ui;
        .picker { @include ui.field; max-width: 480px; }
    `,
})
export class ProjectPickerComponent {
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly statusLabels = PROJECT_STATUS_LABELS;
    // A failed load shows an empty list (the error interceptor has told the user); the next page visit asks again.
    protected readonly projects = toSignal(
        inject(LookupService).projects$.pipe(catchError(() => of([] as ProjectOption[]))),
        { initialValue: [] as ProjectOption[] },
    );

    private readonly projectIdParam = toSignal(
        this.route.queryParamMap.pipe(map(params => Number(params.get('projectId')) || null)),
        { initialValue: null },
    );

    readonly selected = computed<ProjectOption | undefined>(() =>
        this.projects().find(p => p.id === this.projectIdParam()));

    constructor() {
        // Opening the page without a (known) project picks the first active one.
        effect(() => {
            const projects = this.projects();
            if (projects.length && !this.selected()) {
                const first = projects.find(p => p.status === 'Active') ?? projects[0];
                this.choose(first.id);
            }
        });
    }

    protected choose(projectId: number | null): void {
        this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { projectId },
            queryParamsHandling: 'merge',
            replaceUrl: true,
        });
    }
}
