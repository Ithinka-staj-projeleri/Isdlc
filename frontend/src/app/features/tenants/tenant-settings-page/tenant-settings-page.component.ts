import { Component, effect, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { TenantService } from '../tenant.service';

/**
 * /admin/settings (company administrator): the company's AI switches and what they mean.
 * Contract: docs/multi-tenancy.md → Yapay zekâ ayarlarının etkisi.
 */
@Component({
    selector: 'app-tenant-settings-page',
    imports: [FormsModule, EmptyStateComponent],
    templateUrl: './tenant-settings-page.component.html',
    styleUrl: './tenant-settings-page.component.scss',
})
export class TenantSettingsPageComponent {
    private readonly service = inject(TenantService);

    protected readonly settings = rxResource({ stream: () => this.service.getSettings() });

    protected aiEnabled = false;
    protected aiDataSharingAllowed = false;
    protected readonly saving = signal(false);
    protected readonly savedMessage = signal('');

    constructor() {
        effect(() => {
            const value = this.settings.value();
            if (value) {
                this.aiEnabled = value.aiEnabled;
                this.aiDataSharingAllowed = value.aiDataSharingAllowed;
            }
        });
    }

    protected changed(): void {
        this.savedMessage.set('');
    }

    protected save(): void {
        this.saving.set(true);
        this.service.updateSettings({ aiEnabled: this.aiEnabled, aiDataSharingAllowed: this.aiDataSharingAllowed }).subscribe({
            next: value => {
                this.saving.set(false);
                this.settings.set(value);
                this.savedMessage.set('Ayarlar kaydedildi.');
            },
            error: () => this.saving.set(false),
        });
    }
}
