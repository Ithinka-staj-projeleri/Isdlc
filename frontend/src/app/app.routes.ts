import { Routes } from '@angular/router';
import { authGuard, changePasswordGuard, guestGuard, layoutChildGuard, tenantAdminGuard } from './core/auth/auth.guard';
import { LayoutComponent } from './core/layout/layout/layout.component';

export const routes: Routes = [
    {
        path: 'login',
        canActivate: [guestGuard],
        loadComponent: () => import('./features/identity/login-page.component').then(m => m.LoginPageComponent),
    },
    {
        path: 'change-password',
        canActivate: [changePasswordGuard],
        loadComponent: () => import('./features/identity/change-password-page/change-password-page.component').then(m => m.ChangePasswordPageComponent),
    },
    {
        path: '',
        component: LayoutComponent,
        canActivate: [authGuard],
        canActivateChild: [layoutChildGuard],
        children: [
            { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
            { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard-page.component').then(m => m.DashboardPageComponent) },
            // "Talepler" and "Projeler" are the same list with a different filter (docs/projects-api.md, decision 3).
            { path: 'requests/new', loadComponent: () => import('./features/projects/project-form/project-form.component').then(m => m.ProjectFormComponent) },
            { path: 'requests', data: { mode: 'requests' }, loadComponent: () => import('./features/projects/project-list/project-list.component').then(m => m.ProjectListComponent) },
            { path: 'projects/:projectId/edit', loadComponent: () => import('./features/projects/project-form/project-form.component').then(m => m.ProjectFormComponent) },
            { path: 'projects/:projectId', loadComponent: () => import('./features/projects/project-detail/project-detail.component').then(m => m.ProjectDetailComponent) },
            { path: 'projects', data: { mode: 'projects' }, loadComponent: () => import('./features/projects/project-list/project-list.component').then(m => m.ProjectListComponent) },
            { path: 'assessment', loadComponent: () => import('./features/assessment/assessment-page.component').then(m => m.AssessmentPageComponent) },
            { path: 'requirements', loadComponent: () => import('./features/requirements/requirements-page.component').then(m => m.RequirementsPageComponent) },
            { path: 'risks', loadComponent: () => import('./features/risks/risks-page.component').then(m => m.RisksPageComponent) },
            { path: 'open-issues', loadComponent: () => import('./features/open-issues/open-issues-page.component').then(m => m.OpenIssuesPageComponent) },
            { path: 'meetings', loadComponent: () => import('./features/meetings/meetings-page.component').then(m => m.MeetingsPageComponent) },
            { path: 'users/:userId', loadComponent: () => import('./features/identity/user-profile-page/user-profile-page.component').then(m => m.UserProfilePageComponent) },
            { path: 'notifications', loadComponent: () => import('./features/notifications/notifications-page.component').then(m => m.NotificationsPageComponent) },
            { path: 'process-improvements/:id', loadComponent: () => import('./features/process-improvements/improvement-detail/improvement-detail.component').then(m => m.ImprovementDetailComponent) },
            { path: 'process-improvements', loadComponent: () => import('./features/process-improvements/improvement-list/improvement-list.component').then(m => m.ImprovementListComponent) },
            // Faz 6 · T5 (docs/multi-tenancy.md → Ekranlar). layoutChildGuard keeps the Platform Administrator on /platform.
            { path: 'platform/tenants', loadComponent: () => import('./features/tenants/tenant-list-page/tenant-list-page.component').then(m => m.TenantListPageComponent) },
            { path: 'admin/users', canActivate: [tenantAdminGuard], loadComponent: () => import('./features/identity/user-admin-page/user-admin-page.component').then(m => m.UserAdminPageComponent) },
            { path: 'admin/settings', canActivate: [tenantAdminGuard], loadComponent: () => import('./features/tenants/tenant-settings-page/tenant-settings-page.component').then(m => m.TenantSettingsPageComponent) },
            { path: 'admin/request-fields', canActivate: [tenantAdminGuard], loadComponent: () => import('./features/projects/custom-fields/custom-field-admin-page/custom-field-admin-page.component').then(m => m.CustomFieldAdminPageComponent) },
            { path: 'process-library/:code/edit', canActivate: [tenantAdminGuard], loadComponent: () => import('./features/process/flow-editor/flow-editor.component').then(m => m.FlowEditorComponent) },
            { path: 'process-library', loadComponent: () => import('./features/process/process-library/process-library.component').then(m => m.ProcessLibraryComponent) },
        ],
    },
    { path: '**', redirectTo: '' },
];