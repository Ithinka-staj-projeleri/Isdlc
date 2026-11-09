import { UserRole } from './user-role';

/** Kept so existing `import { UserRole } from '.../user-summary'` lines still work; defined in user-role.ts. */
export type { UserRole };

/** Who did something or is responsible for something (backend: UserSummaryDto). */
export interface UserSummary {
  id: number;
  fullName: string;
}

/** A person to pick in a form (backend: GET /api/v1/users). */
export interface UserLookup extends UserSummary {
  role: UserRole;
}
