import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { PERMISSIONS, PERMISSION_LABELS, Permission } from '../../../shared/models/permission';
import { PROJECT_STATUS_LABELS, ProjectStatus } from '../../../shared/models/project-status';
import { DocumentType } from '../../documents/models/document';
import { CustomField } from '../../projects/custom-fields/custom-field';
import {
    ConditionField,
    MAX_PREREQUISITES,
    MAX_RULES,
    NO_OUTCOMES,
    OPERATOR_LABELS,
    PREREQUISITE_KINDS,
    PREREQUISITE_LABELS,
    SaveStepInput,
    conditionFields,
    operatorTakesValue,
    operatorsFor,
    prerequisiteTakesValue,
    ruleValue,
} from '../models/flow-edit';
import { ConditionOperator, PrerequisiteKind, ProcessFlow, ProcessStep } from '../models/process-stage';
import { ProcessService } from '../process.service';

/** Which step the form edits: an existing one, or a new one in a stage after a step (null: first in the stage). */
export interface StepFormTarget {
    step: ProcessStep | null;
    stageId: number;
    afterStepId: number | null;
}

/** A rule as the form holds it: the value as the control gives it, "boşsa sağlanmış say" as a box. */
interface RuleRow {
    field: string;
    op: ConditionOperator;
    value: string | number | null;
    whenEmptyMet: boolean;
}

interface PrerequisiteRow {
    kind: PrerequisiteKind;
    value: string | null;
}

interface JumpRow {
    targetStepId: number | null;
    label: string;
}

/** What "Hayır" does on a decision step: nothing (the step is done again), close the record, or go back. */
type NoMode = 'none' | 'close' | 'return';

/**
 * The step form of the flow editor (Faz 7 · A8): text, who does it, decision and approver, what "Hayır" does, the work
 * it needs (prerequisites) and when it is done (skip rules). The API checks every rule again; its 400 messages appear
 * under the row they belong to ("conditions[1]"). Contract: docs/configurability.md → Akış düzenleme, Bölüm 4 ve 5.
 */
@Component({
    selector: 'app-step-form',
    imports: [FormsModule],
    templateUrl: './step-form.component.html',
    styleUrl: './step-form.component.scss',
})
export class StepFormComponent implements OnInit {
    private readonly processService = inject(ProcessService);

    readonly flow = input.required<ProcessFlow>();
    readonly target = input.required<StepFormTarget>();
    readonly customFields = input<CustomField[]>([]);
    readonly questionSets = input<{ code: string; name: string }[]>([]);
    readonly documentTypes = input<DocumentType[]>([]);
    readonly saved = output<ProcessFlow>();
    readonly cancelled = output<void>();

    protected readonly permissions = PERMISSIONS;
    protected readonly permissionLabels = PERMISSION_LABELS;
    protected readonly outcomes = NO_OUTCOMES;
    protected readonly statusLabels = PROJECT_STATUS_LABELS;
    protected readonly operatorLabels = OPERATOR_LABELS;
    protected readonly prerequisiteKinds = PREREQUISITE_KINDS;
    protected readonly prerequisiteLabels = PREREQUISITE_LABELS;
    protected readonly maxRules = MAX_RULES;
    protected readonly maxPrerequisites = MAX_PREREQUISITES;
    protected readonly takesValue = operatorTakesValue;
    protected readonly prerequisiteTakesValue = prerequisiteTakesValue;

    protected text = '';
    protected responsiblePermission: Permission | null = null;
    protected isDecision = false;
    protected approverPermission: Permission | null = null;
    protected noMode: NoMode = 'none';
    protected noOutcome: ProjectStatus | null = null;
    protected noBranchText = '';
    protected jumps: JumpRow[] = [];
    protected rules: RuleRow[] = [];
    protected prerequisites: PrerequisiteRow[] = [];

    protected readonly saving = signal(false);
    protected readonly submitted = signal(false);
    protected readonly errors = signal<Record<string, string[]>>({});

    /** The record's fields and the company's active custom fields. */
    protected readonly fields = computed(() => conditionFields(this.customFields()));

    /** Earlier active steps of the flow: where "Hayır" may go back to. */
    protected readonly jumpTargets = computed<ProcessStep[]>(() => {
        const target = this.target();
        const stages = this.flow().stages;
        const all = stages.flatMap(s => s.steps);
        let end: number;
        if (target.step) {
            end = all.findIndex(s => s.id === target.step!.id);
        } else if (target.afterStepId !== null) {
            end = all.findIndex(s => s.id === target.afterStepId) + 1;
        } else {
            const stageIndex = stages.findIndex(s => s.id === target.stageId);
            end = stages.slice(0, stageIndex).reduce((n, s) => n + s.steps.length, 0);
        }
        return all.slice(0, Math.max(end, 0)).filter(s => s.isActive);
    });

    protected readonly stageTitle = computed(() =>
        this.flow().stages.find(s => s.id === this.target().stageId)?.title ?? '');

    ngOnInit(): void {
        const step = this.target().step;
        if (!step) return;
        this.text = step.text;
        this.responsiblePermission = step.responsiblePermission;
        this.isDecision = step.isDecision;
        this.approverPermission = step.approverPermission;
        this.noOutcome = step.noOutcome;
        this.noBranchText = step.noBranchText ?? '';
        this.jumps = step.noJumps.map(j => ({ targetStepId: j.targetStepId, label: j.label }));
        this.noMode = step.noOutcome ? 'close' : step.noJumps.length ? 'return' : 'none';
        this.rules = step.conditions.map(rule => ({
            field: rule.field,
            op: rule.op,
            value: rule.value === null || rule.value === undefined ? null
                : typeof rule.value === 'number' ? rule.value : String(rule.value),
            whenEmptyMet: rule.whenEmpty === 'Met',
        }));
        this.prerequisites = step.prerequisites.map(p => ({ kind: p.kind, value: p.value ?? null }));
    }

    // ---- Rows ----

    protected fieldOf(rule: RuleRow): ConditionField | undefined {
        return this.fields().find(f => f.field === rule.field);
    }

    protected operatorsOf(rule: RuleRow): ConditionOperator[] {
        return operatorsFor(this.fieldOf(rule)?.type);
    }

    protected onFieldChange(rule: RuleRow): void {
        rule.op = 'Equals';
        rule.value = null;
    }

    protected addRule(): void {
        this.rules = [...this.rules, { field: this.fields()[0].field, op: 'Equals', value: null, whenEmptyMet: false }];
    }

    protected removeRule(index: number): void {
        this.rules = this.rules.filter((_, i) => i !== index);
        this.errors.set({});
    }

    protected addPrerequisite(): void {
        this.prerequisites = [...this.prerequisites, { kind: 'RequestFormComplete', value: null }];
    }

    protected removePrerequisite(index: number): void {
        this.prerequisites = this.prerequisites.filter((_, i) => i !== index);
        this.errors.set({});
    }

    protected addJump(): void {
        this.jumps = [...this.jumps, { targetStepId: null, label: '' }];
    }

    protected removeJump(index: number): void {
        this.jumps = this.jumps.filter((_, i) => i !== index);
    }

    /** The choices for a prerequisite's value: the company's forms or document types. */
    protected valueOptions(kind: PrerequisiteKind): { code: string; name: string }[] {
        return kind === 'QuestionnaireComplete'
            ? this.questionSets()
            : this.documentTypes().map(d => ({ code: d.code, name: d.name }));
    }

    // ---- Errors ----

    /** The API's first message for the key ("conditions[1]", "text"), then the form's own check after a save attempt. */
    protected errorFor(key: string): string | null {
        const fromServer = this.errors()[key];
        if (fromServer?.length) return fromServer[0];
        if (!this.submitted()) return null;
        switch (key) {
            case 'text': return this.text.trim() ? null : 'Adımın açıklaması zorunludur.';
            case 'responsiblePermission': return this.responsiblePermission ? null : 'Adımı kimin yapacağı seçilmelidir.';
            case 'noOutcome': return this.isDecision && this.noMode === 'close' && !this.noOutcome ? 'Kaydın nasıl kapanacağı seçilmelidir.' : null;
            case 'noJumps': return this.isDecision && this.noMode === 'return' && this.jumps.length === 0 ? 'En az bir dönüş adımı eklenmelidir.' : null;
            default: return null;
        }
    }

    /** Errors whose key has no row on the form (e.g. a rule beyond the limit), so none is lost. */
    protected otherErrors(): string[] {
        return Object.entries(this.errors())
            .filter(([key]) => !this.isShownUnderARow(key))
            .flatMap(([, messages]) => messages);
    }

    private isShownUnderARow(key: string): boolean {
        if (/^(text|responsiblePermission|approverPermission|noOutcome|noBranchText)$/i.test(key)) return true;
        if (/^noJumps$/i.test(key)) return this.isDecision && this.noMode === 'return';
        const match = /^(noJumps|conditions|prerequisites)\[(\d+)\]/i.exec(key);
        if (!match) return false;
        const index = Number(match[2]);
        const rows = { nojumps: this.jumps, conditions: this.rules, prerequisites: this.prerequisites }[match[1].toLowerCase()];
        return !!rows && index < rows.length;
    }

    /** "conditions[1]" and "conditions[1].value" both belong to the row. */
    protected rowError(list: 'conditions' | 'prerequisites', index: number): string | null {
        const prefix = `${list}[${index}]`;
        const entry = Object.entries(this.errors()).find(([key]) => key.toLowerCase().startsWith(prefix.toLowerCase()));
        return entry?.[1][0] ?? null;
    }

    protected jumpError(index: number): string | null {
        const prefix = `noJumps[${index}]`;
        const entry = Object.entries(this.errors()).find(([key]) => key.toLowerCase().startsWith(prefix.toLowerCase()));
        return entry?.[1][0] ?? null;
    }

    // ---- Save ----

    protected save(): void {
        this.submitted.set(true);
        this.errors.set({});
        if (['text', 'responsiblePermission', 'noOutcome', 'noJumps'].some(k => this.errorFor(k))) {
            return;
        }

        const flow = this.flow();
        const target = this.target();
        const input: SaveStepInput = {
            text: this.text.trim(),
            isDecision: this.isDecision,
            noBranchText: this.isDecision && this.noBranchText.trim() ? this.noBranchText.trim() : null,
            responsiblePermission: this.responsiblePermission,
            approverPermission: this.isDecision ? this.approverPermission : null,
            noOutcome: this.isDecision && this.noMode === 'close' ? this.noOutcome : null,
            noJumps: this.isDecision && this.noMode === 'return'
                ? this.jumps.map(j => ({ targetStepId: j.targetStepId === null ? null : Number(j.targetStepId), label: j.label.trim() }))
                : [],
            conditions: this.rules.map(rule => ({
                field: rule.field,
                op: rule.op,
                value: operatorTakesValue(rule.op) ? ruleValue(this.fieldOf(rule)?.type, rule.value) : null,
                whenEmpty: rule.whenEmptyMet ? 'Met' : 'NotMet',
            })),
            prerequisites: this.prerequisites.map(p => ({
                kind: p.kind,
                value: prerequisiteTakesValue(p.kind) ? p.value : null,
            })),
            rowVersion: flow.rowVersion,
        };

        const request = target.step
            ? this.processService.updateStep(target.step.id, input)
            : this.processService.addStep(flow.code, { ...input, stageId: target.stageId, afterStepId: target.afterStepId });

        this.saving.set(true);
        request.subscribe({
            next: saved => {
                this.saving.set(false);
                this.saved.emit(saved);
            },
            // 409 (someone else saved the flow) comes as an alert from the error interceptor.
            error: err => {
                this.saving.set(false);
                this.errors.set(fieldErrorsOf(err));
            },
        });
    }
}
