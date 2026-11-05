import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { PagedResult } from '../models/paged-result';
import { ProjectStatus } from '../models/project-status';
import { UserLookup } from '../models/user-summary';

/** A project to pick in a selector (GET /api/v1/projects, only the fields a selector needs). */
export interface ProjectOption {
    id: number;
    code: string;
    name: string;
    status: ProjectStatus;
}

/**
 * Lists used by selectors in several modules. Loaded once and shared, since they change rarely
 * during a session. The full Projects / Identity screens use their own services.
 */
@Injectable({ providedIn: 'root' })
export class LookupService {
    private readonly http = inject(HttpClient);

    /** Up to 100 projects (the API's page size limit), which is enough for the MVP. */
    readonly projects$: Observable<ProjectOption[]> = this.http
        .get<PagedResult<ProjectOption>>('/api/v1/projects', { params: { page: 1, pageSize: 100 } })
        .pipe(map(result => result.items), shareReplay(1));

    /** Active users, for owner / assignee selectors (GET /api/v1/users). */
    readonly users$: Observable<UserLookup[]> = this.http
        .get<UserLookup[]>('/api/v1/users')
        .pipe(shareReplay(1));
}
