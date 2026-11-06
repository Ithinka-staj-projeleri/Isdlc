import { ProjectStatus } from '../../../shared/models/project-status';
import { UserRole } from '../../../shared/models/user-role';

/** GET /api/v1/process-stages — one of the 13 stages with its steps in flow order. */
export interface ProcessStage {
    id: number;
    order: number;
    title: string;
    shortTitle: string;
    steps: ProcessStep[];
}

export interface ProcessStep {
    id: number;
    code: string;
    order: number;
    text: string;
    isDecision: boolean;
    /** What the "No" answer means, in the prototype's words. */
    noBranchText: string | null;
    responsibleRole: UserRole;
    /** Set on approval steps: only this role can act on the step. */
    approverRole: UserRole | null;
    /** "No" closes the request with this status. */
    noOutcome: ProjectStatus | null;
    /** Steps the flow can return to when the answer is "No". */
    noJumps: ProcessStepJump[];
}

export interface ProcessStepJump {
    targetStepId: number;
    targetStepCode: string;
    label: string;
}
