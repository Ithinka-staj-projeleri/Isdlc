import { ProjectStatus } from '../../../shared/models/project-status';
import { UserSummary } from '../../../shared/models/user-summary';
import { ProcessStep } from './process-stage';

export type ProcessStageState = 'Completed' | 'Current' | 'Pending' | 'Stopped';
export type StepAction = 'Completed' | 'Yes' | 'No' | 'Rejected';

export const STEP_ACTION_LABELS: Record<StepAction, string> = {
    Completed: 'Tamamlandı',
    Yes: 'Evet',
    No: 'Hayır',
    Rejected: 'Reddedildi',
};

/** GET /api/v1/projects/{projectId}/process — where the project stands and what the signed-in user can do. */
export interface ProjectProcess {
    projectId: number;
    projectCode: string;
    /** The flow the project follows. */
    flow: { code: string; name: string };
    /** Steps of the flow whose conditions do not hold for this project; they are skipped. */
    skippedStepIds: number[];
    status: ProjectStatus;
    /** null when the project is not active. */
    currentStep: ProcessStep | null;
    progressPercent: number;
    stages: ProcessStageProgress[];
    availableActions: AvailableActions;
}

export interface ProcessStageProgress {
    stageId: number;
    order: number;
    shortTitle: string;
    stepCount: number;
    state: ProcessStageState;
}

/** Decided by the API from the user's role and the current step; the screen only follows it. */
export interface AvailableActions {
    canComplete: boolean;
    canAnswer: boolean;
    canReject: boolean;
    /** Why the user cannot act on the current step; null when they can. */
    blockedReason: string | null;
}

/** One action taken on a step (process history). */
export interface StepRecord {
    id: number;
    stepId: number;
    stepCode: string;
    action: StepAction;
    /** Only for a "No" answer that went back to an earlier step. */
    jumpedToStepCode: string | null;
    note: string | null;
    performedBy: UserSummary;
    performedAt: string;
}

/** POST .../process/answer */
export interface AnswerStep {
    expectedStepId: number;
    answer: 'Yes' | 'No';
    jumpTargetStepId: number | null;
    note: string | null;
}
