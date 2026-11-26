import { Permission } from './permission';

/** Who did something or is responsible for something (backend: UserSummaryDto). */
export interface UserSummary {
  id: number;
  fullName: string;
}

/** A person to pick in a form (backend: GET /api/v1/users), with their permissions (Faz 7). */
export interface UserLookup extends UserSummary {
  permissions: Permission[];
}
