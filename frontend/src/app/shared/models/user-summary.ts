/** Who did something or is responsible for something (backend: UserSummaryDto). */
export interface UserSummary {
  id: number;
  fullName: string;
}

/** Backend enum UserRole, sent as text. */
export type UserRole =
  | 'BusinessAnalyst'
  | 'SoftwareDepartmentManager'
  | 'ProjectManager'
  | 'SoftwareDeveloper'
  | 'TestSpecialist'
  | 'SalesRepresentative';

/** A person to pick in a form (backend: GET /api/v1/users). */
export interface UserLookup extends UserSummary {
  role: UserRole;
}
