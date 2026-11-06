import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserRole } from '../../shared/models/user-role';
import { AuthService } from './auth.service';

// Kullanım: canActivate: [roleGuard('ProjectManager', 'SoftwareDepartmentManager')]
export const roleGuard = (...roles: UserRole[]): CanActivateFn => () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.hasRole(...roles) ? true : router.createUrlTree(['/dashboard']);
};
