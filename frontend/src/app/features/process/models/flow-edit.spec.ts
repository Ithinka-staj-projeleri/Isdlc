import { CustomField } from '../../projects/custom-fields/custom-field';
import {
    FlowOrder,
    conditionFields,
    isRuleOnInactiveField,
    moveStage,
    moveStep,
    operatorsFor,
    ruleValue,
} from './flow-edit';

const order: FlowOrder = {
    stages: [
        { id: 1, stepIds: [11, 12] },
        { id: 2, stepIds: [21] },
        { id: 3, stepIds: [] },
    ],
};

const field = (key: string, isActive: boolean, type: CustomField['type'] = 'Select'): CustomField => ({
    id: 1, key, label: key, type, options: type === 'Select' ? ['Ege'] : [], isRequired: false, helpText: null,
    order: 1, isActive,
});

describe('flow editing (Faz 7 · A8)', () => {
    it('moves a step inside its stage and past the stage edge', () => {
        expect(moveStep(order, 12, -1).stages[0].stepIds).toEqual([12, 11]);
        // Down from the last step of stage 1: first step of stage 2.
        const down = moveStep(order, 12, 1);
        expect(down.stages[0].stepIds).toEqual([11]);
        expect(down.stages[1].stepIds).toEqual([12, 21]);
        // Up from the first step of stage 2: last step of stage 1.
        expect(moveStep(order, 21, -1).stages[0].stepIds).toEqual([11, 12, 21]);
        // Into an empty stage.
        expect(moveStep(order, 21, 1).stages[2].stepIds).toEqual([21]);
    });

    it('does nothing at the ends of the flow', () => {
        expect(moveStep(order, 11, -1)).toBe(order);
        expect(moveStage(order, 1, -1)).toBe(order);
        expect(moveStage(order, 3, 1)).toBe(order);
    });

    it('swaps stages with their steps', () => {
        expect(moveStage(order, 2, -1).stages.map(s => s.id)).toEqual([2, 1, 3]);
        expect(moveStage(order, 2, -1).stages[0].stepIds).toEqual([21]);
    });

    it('offers only the active custom fields, and "en az / en çok" only for numbers and dates', () => {
        const fields = conditionFields([field('bolge', true), field('eski', false)]);
        expect(fields.map(f => f.field)).toContain('custom:bolge');
        expect(fields.map(f => f.field)).not.toContain('custom:eski');
        expect(fields.find(f => f.field === 'custom:bolge')?.type).toBe('Choice');
        expect(operatorsFor('Choice')).not.toContain('AtLeast');
        expect(operatorsFor('Date')).toContain('AtMost');
    });

    it('warns about a rule on a switched-off or unknown field', () => {
        const fields = [field('bolge', true), field('eski', false)];
        expect(isRuleOnInactiveField({ field: 'custom:bolge', op: 'Equals', value: 'Ege' }, fields)).toBe(false);
        expect(isRuleOnInactiveField({ field: 'custom:eski', op: 'Equals', value: 'Ege' }, fields)).toBe(true);
        expect(isRuleOnInactiveField({ field: 'custom:yok', op: 'IsSet' }, fields)).toBe(true);
        expect(isRuleOnInactiveField({ field: 'source', op: 'Equals', value: 'External' }, fields)).toBe(false);
    });

    it('sends values in the type the API expects', () => {
        expect(ruleValue('Boolean', 'true')).toBe(true);
        expect(ruleValue('Number', '2')).toBe(2);
        expect(ruleValue('Choice', ' External ')).toBe('External');
        expect(ruleValue('Date', '')).toBeNull();
    });
});
