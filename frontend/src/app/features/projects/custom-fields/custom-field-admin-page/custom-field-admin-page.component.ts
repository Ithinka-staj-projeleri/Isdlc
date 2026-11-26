import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../../shared/components/status-badge/status-badge.component';
import { CUSTOM_FIELD_TYPE_LABELS, CustomField } from '../custom-field';
import { CustomFieldService } from '../custom-field.service';
import { CustomFieldFormComponent } from '../custom-field-form/custom-field-form.component';

/**
 * /admin/request-fields (company administrator): the fields the company adds to its request form (Faz 7 · A6),
 * inactive ones too; add, edit, deactivate. Contract: docs/configurability.md → Şirkete özel alanlar.
 */
@Component({
  selector: 'app-custom-field-admin-page',
  imports: [EmptyStateComponent, StatusBadgeComponent, CustomFieldFormComponent],
  templateUrl: './custom-field-admin-page.component.html',
  styleUrl: './custom-field-admin-page.component.scss',
})
export class CustomFieldAdminPageComponent {
  private readonly service = inject(CustomFieldService);

  protected readonly typeLabels = CUSTOM_FIELD_TYPE_LABELS;
  protected readonly fields = rxResource({ stream: () => this.service.list(true) });

  /** undefined: no form; null: new field. */
  protected readonly editing = signal<CustomField | null | undefined>(undefined);

  protected onSaved(): void {
    this.editing.set(undefined);
    this.fields.reload();
  }
}
