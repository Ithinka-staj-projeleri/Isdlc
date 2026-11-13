import { ProjectStatus } from '../../../shared/models/project-status';
import { UserSummary } from '../../../shared/models/user-summary';
import { ProcessFlowKind, ProcessStep } from './process-stage';
import { RequestDecisionForm } from './request-decision';

export type ProcessStageState = 'Completed' | 'Current' | 'Pending' | 'Stopped';
export type StepAction = 'Completed' | 'Yes' | 'No' | 'Rejected' | 'Postponed' | 'Resumed';

export const STEP_ACTION_LABELS: Record<StepAction, string> = {
    Completed: 'Tamamlandı',
    Yes: 'Evet',
    No: 'Hayır',
    Rejected: 'Reddedildi',
    Postponed: 'Ertelendi',
    Resumed: 'Değerlendirmeye geri alındı',
};

/** GET /api/v1/projects/{projectId}/process — where the project stands and what the signed-in user can do. */
export interface ProjectProcess {
    projectId: number;
    projectCode: string;
    /** The flow the project follows. */
    flow: { code: string; name: string; kind: ProcessFlowKind };
    /** Steps of the flow whose conditions do not hold for this project; they are skipped. */
    skippedStepIds: number[];
    status: ProjectStatus;
    /** The step it is on (also while postponed); null when the project is closed. */
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
    /** A request (before the project stage) can be put on hold. */
    canPostpone: boolean;
    /** A postponed request can be taken back. */
    canResume: boolean;
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
    /** The Talep Ret / Erteleme Formu; required for a "No" that closes a request. */
    closingDecision?: RequestDecisionForm | null;
}
