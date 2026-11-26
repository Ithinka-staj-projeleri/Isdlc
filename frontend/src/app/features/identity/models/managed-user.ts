import { UserRole } from '../../../shared/models/user-role';

// Contract: docs/multi-tenancy.md → Uç noktalar, Şirket Yöneticisi (Faz 6 · T4).

/** A user of the company as the company administrator sees them (backend: UserDto, GET /users/manage). */
export interface ManagedUser {
    id: number;
    fullName: string;
    email: string;
    /** Worked out from permissions until A3 (UserAdminService). */
    role: UserRole;
    permissions?: string[];
    isActive: boolean;
    isTenantAdmin: boolean;
    /** Has a temporary password they have not changed yet. */
    mustChangePassword: boolean;
}

/** POST /users. */
export interface CreateUserInput {
    fullName: string;
    email: string;
    role: UserRole;
    isTenantAdmin: boolean;
    temporaryPassword: string;
}

/** PUT /users/{id}: everything but the e-mail, which is the sign-in name and never changes. */
export interface UpdateUserInput {
    fullName: string;
    role: UserRole;
    isActive: boolean;
    isTenantAdmin: boolean;
}
