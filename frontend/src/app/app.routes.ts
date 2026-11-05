import { Routes } from '@angular/router';
import { LayoutComponent } from './core/layout/layout/layout.component';

export const routes: Routes = [
    {
        path: '',
        component: LayoutComponent,
        children: [
            { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
            { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard-page.component').then(m => m.DashboardPageComponent) },
            { path: 'projects', loadComponent: () => import('./features/projects/projects-page.component').then(m => m.ProjectsPageComponent) },
            { path: 'assessment', loadComponent: () => import('./features/assessment/assessment-page.component').then(m => m.AssessmentPageComponent) },
            { path: 'requirements', loadComponent: () => import('./features/requirements/requirements-page.component').then(m => m.RequirementsPageComponent) },
            { path: 'risks', loadComponent: () => import('./features/risks/risks-page.component').then(m => m.RisksPageComponent) },
            { path: 'open-issues', loadComponent: () => import('./features/open-issues/open-issues-page.component').then(m => m.OpenIssuesPageComponent) },
            { path: 'meetings', loadComponent: () => import('./features/meetings/meetings-page.component').then(m => m.MeetingsPageComponent) },
        ],
    },
];