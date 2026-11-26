import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { fieldErrorsOf } from '../../../../core/http/problem-details';
import {
  CUSTOM_FIELD_KEY_PATTERN,
  CUSTOM_FIELD_TYPE_LABELS,
  CustomField,
  CustomFieldType,
  keyFromLabel,
} from '../custom-field';
import { CustomFieldService } from '../custom-field.service';

/**
 * New company field or editing one: label, options (Select), required, help text, order, active.
 * The key and the type are chosen once and never change. Contract: docs/configurability.md → Şirkete özel alanlar.
 */
@Component({
  selector: 'app-custom-field-form',
  imports: [FormsModule],
  templateUrl: './custom-field-form.component.html',
  styleUrl: './custom-field-form.component.scss',
})
export class CustomFieldFormComponent implements OnInit {
  private readonly service = inject(CustomFieldService);

  /** null opens a new field. */
  readonly field = input<CustomField | null>(null);
  readonly saved = output<CustomField>();
  readonly cancelled = output<void>();

  protected readonly typeOptions = Object.entries(CUSTOM_FIELD_TYPE_LABELS) as [CustomFieldType, string][];

  protected label = '';
  protected key = '';
  /** Until the key is typed by hand, it follows the label. */
  protected keyTouched = false;
  protected type: CustomFieldType = 'Text';
  /** One option per line. */
  protected optionsText = '';
  protected isRequired = false;
  protected helpText = '';
  protected order: number | null = null;
  protected isActive = true;

  protected readonly saving = signal(false);
  protected readonly submitted = signal(false);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  ngOnInit(): void {
    const current = this.field();
    if (current) {
      this.label = current.label;
      this.key = current.key;
      this.type = current.type;
      this.optionsText = current.options.join('\n');
      this.isRequired = current.isRequired;
      this.helpText = current.helpText ?? '';
      this.order = current.order;
      this.isActive = current.isActive;
    }
  }

  protected onLabelChange(): void {
    if (!this.field() && !this.keyTouched) {
      this.key = keyFromLabel(this.label);
    }
  }

  protected options(): string[] {
    return this.type === 'Select'
      ? this.optionsText.split('\n').map((o) => o.trim()).filter((o) => o !== '')
      : [];
  }

  /** The API's first message for the field, then the form's own check after a save attempt. */
  protected errorFor(name: string): string | null {
    const fromServer = this.serverErrors()[name];
    if (fromServer?.length) return fromServer[0];
    if (!this.submitted()) return null;

    switch (name) {
      case 'label':
        return this.label.trim() ? null : 'Alan adı zorunludur.';
      case 'key':
        if (this.field()) return null;
        return CUSTOM_FIELD_KEY_PATTERN.test(this.key)
          ? null
          : 'Alan anahtarı 2–50 karakter olmalı; yalnızca küçük harf, rakam ve tire kullanılabilir.';
      case 'options': {
        if (this.type !== 'Select') return null;
        const options = this.options();
        if (options.length === 0) return 'Seçim alanının en az bir seçeneği olmalıdır.';
        if (options.length > 50) return 'En fazla 50 seçenek eklenebilir.';
        if (options.some((o) => o.length > 100)) return 'Her seçenek 1 ile 100 karakter arasında olmalıdır.';
        const lower = options.map((o) => o.toLocaleLowerCase('tr-TR'));
        return new Set(lower).size === lower.length ? null : 'Aynı seçenek iki kez yazılamaz.';
      }
      default:
        return null;
    }
  }

  protected save(): void {
    this.submitted.set(true);
    this.serverErrors.set({});
    if (['label', 'key', 'options'].some((f) => this.errorFor(f))) {
      return;
    }

    const common = {
      label: this.label.trim(),
      options: this.options(),
      isRequired: this.isRequired,
      helpText: this.helpText.trim() || null,
      order: this.order,
    };
    const current = this.field();
    const request = current
      ? this.service.update(current.id, { ...common, isActive: this.isActive })
      : this.service.create({ ...common, key: this.key, type: this.type });

    this.saving.set(true);
    request.subscribe({
      next: (field) => {
        this.saving.set(false);
        this.saved.emit(field);
      },
      // 409 (key taken, option used in a skip rule) comes as an alert from the error interceptor.
      error: (err) => {
        this.saving.set(false);
        this.serverErrors.set(fieldErrorsOf(err));
      },
    });
  }
}
