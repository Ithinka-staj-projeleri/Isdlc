import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, PLATFORM_HOME } from './auth.service';

/** Sends a signed-out user to the login page and brings them back to the same page after sign-in. */
export const authGuard: CanActivateFn = (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.isLoggedIn()
        ? true
        : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Keeps a signed-in user off the login page. */
export const guestGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.isLoggedIn() ? router.createUrlTree([auth.homeUrl()]) : true;
};

/**
 * Pages inside the layout. A temporary password closes them until it is changed (the API answers 403 anyway);
 * the Platform Administrator has no company, so they see only the company pages (docs/multi-tenancy.md → Ekranlar).
 */
export const layoutChildGuard: CanActivateFn = (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (auth.mustChangePassword()) {
        return router.createUrlTree(['/change-password']);
    }
    const onPlatformPage = state.url.startsWith('/platform');
    if (auth.isPlatformAdmin() !== onPlatformPage) {
        return router.createUrlTree([auth.isPlatformAdmin() ? PLATFORM_HOME : '/dashboard']);
    }
    return true;
};

/** The password change page: signed in, and only while there is a temporary password or by choice. */
export const changePasswordGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.isLoggedIn() ? true : router.createUrlTree(['/login']);
};

/** Company administrator pages (/admin/...). */
export const tenantAdminGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.isTenantAdmin() ? true : router.createUrlTree(['/dashboard']);
};
