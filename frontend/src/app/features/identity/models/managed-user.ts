import { Permission } from '../../../shared/models/permission';

// Contract: docs/multi-tenancy.md → Uç noktalar, Şirket Yöneticisi (Faz 6 · T4); permissions: docs/configurability.md (Faz 7).

/** A user of the company as the company administrator sees them (backend: UserDto, GET /users/manage). */
export interface ManagedUser {
    id: number;
    fullName: string;
    email: string;
    /** What the user can do in the flow; empty: work outside the flow only. */
    permissions: Permission[];
    isActive: boolean;
    isTenantAdmin: boolean;
    /** Has a temporary password they have not changed yet. */
    mustChangePassword: boolean;
}

/** POST /users. */
export interface CreateUserInput {
    fullName: string;
    email: string;
    permissions: Permission[];
    isTenantAdmin: boolean;
    temporaryPassword: string;
}

/** PUT /users/{id}: everything but the e-mail, which is the sign-in name and never changes. The list is sent whole. */
export interface UpdateUserInput {
    fullName: string;
    permissions: Permission[];
    isActive: boolean;
    isTenantAdmin: boolean;
}
