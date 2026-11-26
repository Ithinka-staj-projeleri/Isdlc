import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { PERMISSIONS, PERMISSION_LABELS, Permission, PermissionPreset, matchingPreset } from '../../../shared/models/permission';
import { PASSWORD_MIN_LENGTH, generateTemporaryPassword } from '../models/password';
import { ManagedUser } from '../models/managed-user';
import { UserAdminService } from '../user-admin.service';

/**
 * New user of the company (with a temporary password) or editing one: name, permissions, active, company administrator.
 * The e-mail is the sign-in name and never changes. Permissions are boxes; a ready-made set only ticks them
 * (docs/configurability.md → Ekranlar). Messages: docs/multi-tenancy.md → Hata mesajları.
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

    /** The API's list and sets; until it answers (or if it fails) the boxes use the built-in names and there are no sets. */
    private readonly catalog = toSignal(this.service.catalog().pipe(catchError(() => of(null))), { initialValue: null });
    protected readonly permissionOptions = computed(() =>
        this.catalog()?.permissions ?? PERMISSIONS.map(value => ({ value, name: PERMISSION_LABELS[value] })));
    protected readonly presets = computed(() => this.catalog()?.presets ?? []);

    /** The API refuses deactivating one's own account; the form does not offer it. */
    protected readonly isSelf = computed(() => this.user()?.id === this.auth.user()?.id);

    protected fullName = '';
    protected email = '';
    protected readonly permissions = signal<Permission[]>([]);
    /** The set whose permissions are exactly the ticked ones, shown in the set selector. */
    protected readonly preset = computed(() => matchingPreset(this.presets(), this.permissions()));
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
            this.permissions.set([...current.permissions]);
            this.isActive = current.isActive;
            this.isTenantAdmin = current.isTenantAdmin;
        }
    }

    protected has(permission: Permission): boolean {
        return this.permissions().includes(permission);
    }

    protected toggle(permission: Permission, on: boolean): void {
        this.permissions.update(list => on ? [...list.filter(p => p !== permission), permission] : list.filter(p => p !== permission));
    }

    /** Ticks exactly the set's boxes; they can still be changed one by one. */
    protected applyPreset(name: string): void {
        const preset = this.presets().find((p: PermissionPreset) => p.name === name);
        if (preset) {
            this.permissions.set([...preset.permissions]);
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
            case 'temporaryPassword':
                if (this.user()) return null;
                return this.temporaryPassword.length >= PASSWORD_MIN_LENGTH ? null : `Parola en az ${PASSWORD_MIN_LENGTH} karakter olmalıdır.`;
            default: return null;
        }
    }

    protected save(): void {
        this.submitted.set(true);
        this.serverErrors.set({});
        if (['fullName', 'email', 'temporaryPassword'].some(f => this.errorFor(f))) {
            return;
        }

        // In the list's order, so the saved order does not depend on the clicking order.
        const permissions = PERMISSIONS.filter(p => this.has(p));
        const current = this.user();
        const request = current
            ? this.service.update(current.id, {
                fullName: this.fullName.trim(),
                permissions,
                isActive: this.isActive,
                isTenantAdmin: this.isTenantAdmin,
            })
            : this.service.create({
                fullName: this.fullName.trim(),
                email: this.email.trim(),
                permissions,
                isTenantAdmin: this.isTenantAdmin,
                temporaryPassword: this.temporaryPassword,
            });

        this.saving.set(true);
        request.subscribe({
            next: user => {
                this.saving.set(false);
                // The administrator changed their own permissions: the buttons follow at once.
                if (this.isSelf()) {
                    this.auth.refreshMe();
                }
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
