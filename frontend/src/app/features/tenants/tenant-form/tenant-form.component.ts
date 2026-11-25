import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { ASSIGNABLE_ROLES, USER_ROLE_LABELS, UserRole } from '../../../shared/models/user-role';
import { PASSWORD_MIN_LENGTH, generateTemporaryPassword } from '../../identity/models/password';
import { Tenant, isValidTenantCode, suggestTenantCode } from '../models/tenant';
import { TenantService } from '../tenant.service';

/**
 * Platform Administrator: a new company with its first administrator, or a company's name and active switch.
 * The code never changes. Contract: docs/multi-tenancy.md → Uç noktalar, Platform Yöneticisi.
 */
@Component({
    selector: 'app-tenant-form',
    imports: [FormsModule],
    templateUrl: './tenant-form.component.html',
    styleUrl: './tenant-form.component.scss',
})
export class TenantFormComponent implements OnInit {
    private readonly service = inject(TenantService);

    /** null opens a new company. */
    readonly tenant = input<Tenant | null>(null);
    readonly saved = output<{ tenant: Tenant; adminEmail?: string; temporaryPassword?: string }>();
    readonly cancelled = output<void>();

    protected readonly roles = ASSIGNABLE_ROLES;
    protected readonly roleLabels = USER_ROLE_LABELS;

    protected name = '';
    protected code = '';
    /** Until the user types a code of their own, the code follows the name. */
    private codeTouched = false;
    protected isActive = true;
    protected adminFullName = '';
    protected adminEmail = '';
    protected adminRole: UserRole | null = 'BusinessAnalyst';
    protected temporaryPassword = generateTemporaryPassword();

    protected readonly saving = signal(false);
    protected readonly submitted = signal(false);
    protected readonly serverErrors = signal<Record<string, string[]>>({});

    ngOnInit(): void {
        const current = this.tenant();
        if (current) {
            this.name = current.name;
            this.code = current.code;
            this.isActive = current.isActive;
        }
    }

    protected onNameChange(name: string): void {
        this.name = name;
        if (!this.tenant() && !this.codeTouched) {
            this.code = suggestTenantCode(name);
        }
    }

    protected onCodeChange(code: string): void {
        this.code = code;
        this.codeTouched = true;
    }

    protected newPassword(): void {
        this.temporaryPassword = generateTemporaryPassword();
    }

    /** Server keys are the JSON field names ("name", "admin.email"); then the form's own check. */
    protected errorFor(field: string): string | null {
        const fromServer = this.serverErrors()[field];
        if (fromServer?.length) return fromServer[0];
        if (!this.submitted()) return null;

        const isNew = !this.tenant();
        switch (field) {
            case 'name': return this.name.trim() ? null : 'Şirket adı zorunludur.';
            case 'code':
                return !isNew || isValidTenantCode(this.code)
                    ? null : 'Şirket kodu küçük harf, rakam ve tireden oluşmalıdır (2–50 karakter).';
            case 'admin.fullName': return !isNew || this.adminFullName.trim() ? null : 'Ad soyad zorunludur.';
            case 'admin.email':
                return !isNew || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.adminEmail.trim()) ? null : 'Geçerli bir e-posta adresi girin.';
            case 'admin.role': return !isNew || this.adminRole ? null : 'Rol seçilmelidir.';
            case 'admin.temporaryPassword':
                return !isNew || this.temporaryPassword.length >= PASSWORD_MIN_LENGTH
                    ? null : `Parola en az ${PASSWORD_MIN_LENGTH} karakter olmalıdır.`;
            default: return null;
        }
    }

    protected save(): void {
        this.submitted.set(true);
        this.serverErrors.set({});
        const fields = ['name', 'code', 'admin.fullName', 'admin.email', 'admin.role', 'admin.temporaryPassword'];
        if (fields.some(f => this.errorFor(f))) {
            return;
        }

        const current = this.tenant();
        this.saving.set(true);
        if (current) {
            this.service.update(current.id, { name: this.name.trim(), isActive: this.isActive }).subscribe({
                next: tenant => this.done({ tenant }),
                error: err => this.failed(err),
            });
            return;
        }

        const adminEmail = this.adminEmail.trim();
        const temporaryPassword = this.temporaryPassword;
        this.service.create({
            code: this.code,
            name: this.name.trim(),
            admin: { fullName: this.adminFullName.trim(), email: adminEmail, role: this.adminRole!, temporaryPassword },
        }).subscribe({
            next: tenant => this.done({ tenant, adminEmail, temporaryPassword }),
            error: err => this.failed(err),
        });
    }

    private done(result: { tenant: Tenant; adminEmail?: string; temporaryPassword?: string }): void {
        this.saving.set(false);
        this.saved.emit(result);
    }

    // Business rules ("Bu şirket kodu kullanılıyor.") come as an alert from the error interceptor.
    private failed(err: unknown): void {
        this.saving.set(false);
        this.serverErrors.set(normalizeKeys(fieldErrorsOf(err)));
    }
}

/** ASP.NET may send nested keys as "Admin.Email" or "admin.email"; the form uses the camelCase path. */
function normalizeKeys(errors: Record<string, string[]>): Record<string, string[]> {
    return Object.fromEntries(Object.entries(errors).map(([key, value]) => [
        key.split('.').map(part => part.charAt(0).toLowerCase() + part.slice(1)).join('.'),
        value,
    ]));
}
