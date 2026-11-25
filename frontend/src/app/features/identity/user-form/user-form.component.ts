import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { ASSIGNABLE_ROLES, USER_ROLE_LABELS, UserRole } from '../../../shared/models/user-role';
import { PASSWORD_MIN_LENGTH, generateTemporaryPassword } from '../models/password';
import { ManagedUser } from '../models/managed-user';
import { UserAdminService } from '../user-admin.service';

/**
 * New user of the company (with a temporary password) or editing one: name, role, active, company administrator.
 * The e-mail is the sign-in name and never changes. Messages: docs/multi-tenancy.md → Hata mesajları.
 */
@Component({
    selector: 'app-user-form',
    imports: [FormsModule],
    templateUrl: './user-form.component.html',
    styleUrl: './user-form.component.scss',
})
export class UserFormComponent implements OnInit {
    private readonly service = inject(UserAdminService);
    private readonly auth = inject(AuthService);

    /** null opens a new user. */
    readonly user = input<ManagedUser | null>(null);
    readonly saved = output<ManagedUser>();
    readonly cancelled = output<void>();

    protected readonly roles = ASSIGNABLE_ROLES;
    protected readonly roleLabels = USER_ROLE_LABELS;
    /** The API refuses deactivating one's own account; the form does not offer it. */
    protected readonly isSelf = computed(() => this.user()?.id === this.auth.user()?.id);

    protected fullName = '';
    protected email = '';
    protected role: UserRole | null = null;
    protected isActive = true;
    protected isTenantAdmin = false;
    protected temporaryPassword = generateTemporaryPassword();

    protected readonly saving = signal(false);
    protected readonly submitted = signal(false);
    protected readonly serverErrors = signal<Record<string, string[]>>({});

    ngOnInit(): void {
        const current = this.user();
        if (current) {
            this.fullName = current.fullName;
            this.email = current.email;
            this.role = current.role;
            this.isActive = current.isActive;
            this.isTenantAdmin = current.isTenantAdmin;
        }
    }

    protected newPassword(): void {
        this.temporaryPassword = generateTemporaryPassword();
    }

    /** The API's first message for the field, then the form's own check after a save attempt. */
    protected errorFor(field: string): string | null {
        const fromServer = this.serverErrors()[field];
        if (fromServer?.length) return fromServer[0];
        if (!this.submitted()) return null;

        switch (field) {
            case 'fullName': return this.fullName.trim() ? null : 'Ad soyad zorunludur.';
            case 'email':
                if (this.user()) return null;
                return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.email.trim()) ? null : 'Geçerli bir e-posta adresi girin.';
            case 'role': return this.role ? null : 'Rol seçilmelidir.';
            case 'temporaryPassword':
                if (this.user()) return null;
                return this.temporaryPassword.length >= PASSWORD_MIN_LENGTH ? null : `Parola en az ${PASSWORD_MIN_LENGTH} karakter olmalıdır.`;
            default: return null;
        }
    }

    protected save(): void {
        this.submitted.set(true);
        this.serverErrors.set({});
        if (['fullName', 'email', 'role', 'temporaryPassword'].some(f => this.errorFor(f))) {
            return;
        }

        const current = this.user();
        const request = current
            ? this.service.update(current.id, {
                fullName: this.fullName.trim(),
                role: this.role!,
                isActive: this.isActive,
                isTenantAdmin: this.isTenantAdmin,
            })
            : this.service.create({
                fullName: this.fullName.trim(),
                email: this.email.trim(),
                role: this.role!,
                isTenantAdmin: this.isTenantAdmin,
                temporaryPassword: this.temporaryPassword,
            });

        this.saving.set(true);
        request.subscribe({
            next: user => {
                this.saving.set(false);
                this.saved.emit(user);
            },
            // Business rules ("Bu e-posta ile bir kullanıcı zaten var.") come as an alert from the error interceptor.
            error: err => {
                this.saving.set(false);
                this.serverErrors.set(fieldErrorsOf(err));
            },
        });
    }
}
