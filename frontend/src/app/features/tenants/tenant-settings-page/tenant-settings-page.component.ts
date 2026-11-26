import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { AI_ASSISTANT_LABELS } from '../models/tenant';
import { TenantService } from '../tenant.service';

/**
 * /admin/settings (company administrator): the company's AI switches and what they mean, its model and monthly limit,
 * and this month's usage (Faz 7 · A9). Contracts: docs/multi-tenancy.md → Yapay zekâ ayarlarının etkisi,
 * docs/configurability.md → Yapay zekâ modeli ve kullanımı.
 */
@Component({
    selector: 'app-tenant-settings-page',
    imports: [FormsModule, DecimalPipe, EmptyStateComponent],
    templateUrl: './tenant-settings-page.component.html',
    styleUrl: './tenant-settings-page.component.scss',
})
export class TenantSettingsPageComponent {
    private readonly service = inject(TenantService);

    protected readonly settings = rxResource({ stream: () => this.service.getSettings() });
    protected readonly models = rxResource({ stream: () => this.service.aiModels() });
    protected readonly usage = rxResource({ stream: () => this.service.aiUsage() });
    protected readonly assistantLabels = AI_ASSISTANT_LABELS;

    protected aiEnabled = false;
    protected aiDataSharingAllowed = false;
    /** '' is the service default. */
    protected aiModel = '';
    protected aiMonthlyTokenLimit: number | null = null;
    protected readonly saving = signal(false);
    protected readonly savedMessage = signal('');
    protected readonly errors = signal<Record<string, string[]>>({});

    /** The service's models; the saved one stays in the list even when the service no longer offers it (or is off). */
    protected readonly modelOptions = computed(() => {
        const offered = this.models.hasValue() ? this.models.value() : [];
        const saved = this.settings.hasValue() ? this.settings.value().aiModel : null;
        return saved && !offered.includes(saved) ? [saved, ...offered] : offered;
    });

    /** Share of the limit used this month, 0–100; null without a limit. */
    protected readonly usedPercent = computed(() => {
        const u = this.usage.hasValue() ? this.usage.value() : undefined;
        if (!u?.limit) return null;
        return Math.min(100, Math.round(((u.inputTokens + u.outputTokens) / u.limit) * 100));
    });

    constructor() {
        effect(() => {
            const value = this.settings.hasValue() ? this.settings.value() : undefined;
            if (value) {
                this.aiEnabled = value.aiEnabled;
                this.aiDataSharingAllowed = value.aiDataSharingAllowed;
                this.aiModel = value.aiModel ?? '';
                this.aiMonthlyTokenLimit = value.aiMonthlyTokenLimit;
            }
        });
    }

    protected changed(): void {
        this.savedMessage.set('');
        this.errors.set({});
    }

    protected errorFor(field: string): string | null {
        return this.errors()[field]?.[0] ?? null;
    }

    protected save(): void {
        this.saving.set(true);
        this.errors.set({});
        const limit = this.aiMonthlyTokenLimit;
        this.service.updateSettings({
            aiEnabled: this.aiEnabled,
            aiDataSharingAllowed: this.aiDataSharingAllowed,
            aiModel: this.aiModel || null,
            // An empty number box gives null (or ''): no limit.
            aiMonthlyTokenLimit: typeof limit === 'number' && Number.isFinite(limit) ? limit : null,
        }).subscribe({
            next: value => {
                this.saving.set(false);
                this.settings.set(value);
                this.usage.reload();
                this.savedMessage.set('Ayarlar kaydedildi.');
            },
            error: err => {
                this.saving.set(false);
                this.errors.set(fieldErrorsOf(err));
            },
        });
    }
}
