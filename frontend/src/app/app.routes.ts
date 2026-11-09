import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/auth.guard';
import { roleGuard } from './core/auth/role.guard';
import { LayoutComponent } from './core/layout/layout/layout.component';

export const routes: Routes = [
    {
        path: 'login',
        canActivate: [guestGuard],
        loadComponent: () => import('./features/identity/login-page.component').then(m => m.LoginPageComponent),
    },
    {
        path: '',
        component: LayoutComponent,
        canActivate: [authGuard],
        children: [
            { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
            { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard-page.component').then(m => m.DashboardPageComponent) },
            // "Talepler" and "Projeler" are the same list with a different filter (docs/projects-api.md, decision 3).
            { path: 'requests/new', canActivate: [roleGuard('BusinessAnalyst')], loadComponent: () => import('./features/projects/project-form/project-form.component').then(m => m.ProjectFormComponent) },
            { path: 'requests', data: { mode: 'requests' }, loadComponent: () => import('./features/projects/project-list/project-list.component').then(m => m.ProjectListComponent) },
            { path: 'projects/:projectId/edit', loadComponent: () => import('./features/projects/project-form/project-form.component').then(m => m.ProjectFormComponent) },
            { path: 'projects/:projectId', loadComponent: () => import('./features/projects/project-detail/project-detail.component').then(m => m.ProjectDetailComponent) },
            { path: 'projects', data: { mode: 'projects' }, loadComponent: () => import('./features/projects/project-list/project-list.component').then(m => m.ProjectListComponent) },
            { path: 'assessment', loadComponent: () => import('./features/assessment/assessment-page.component').then(m => m.AssessmentPageComponent) },
            { path: 'requirements', loadComponent: () => import('./features/requirements/requirements-page.component').then(m => m.RequirementsPageComponent) },
            { path: 'risks', loadComponent: () => import('./features/risks/risks-page.component').then(m => m.RisksPageComponent) },
            { path: 'open-issues', loadComponent: () => import('./features/open-issues/open-issues-page.component').then(m => m.OpenIssuesPageComponent) },
            { path: 'meetings', loadComponent: () => import('./features/meetings/meetings-page.component').then(m => m.MeetingsPageComponent) },
            { path: 'process-library', loadComponent: () => import('./features/process/process-library/process-library.component').then(m => m.ProcessLibraryComponent) },
        ],
    },
    { path: '**', redirectTo: '' },
];