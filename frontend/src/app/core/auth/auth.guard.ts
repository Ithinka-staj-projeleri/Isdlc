import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

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
    return auth.isLoggedIn() ? router.createUrlTree(['/dashboard']) : true;
};
