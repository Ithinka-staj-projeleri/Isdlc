import { Component, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldErrorsOf } from '../../../core/http/problem-details';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { USER_ROLE_LABELS } from '../../../shared/models/user-role';
import { PASSWORD_MIN_LENGTH, generateTemporaryPassword } from '../models/password';
import { ManagedUser } from '../models/managed-user';
import { UserAdminService } from '../user-admin.service';
import { UserFormComponent } from '../user-form/user-form.component';

/**
 * /admin/users (company administrator): every user of the company, inactive ones too; add, edit, give a temporary
 * password. Contract: docs/multi-tenancy.md → Ekranlar.
 */
@Component({
    selector: 'app-user-admin-page',
    imports: [FormsModule, EmptyStateComponent, StatusBadgeComponent, UserFormComponent],
    templateUrl: './user-admin-page.component.html',
    styleUrl: './user-admin-page.component.scss',
})
export class UserAdminPageComponent {
    private readonly service = inject(UserAdminService);

    protected readonly roleLabels = USER_ROLE_LABELS;
    protected readonly currentUserId = inject(AuthService).user()?.id;

    protected readonly users = rxResource({ stream: () => this.service.list() });

    /** undefined: no form; null: new user. */
    protected readonly editing = signal<ManagedUser | null | undefined>(undefined);

    /** The user whose temporary password is being reset. */
    protected readonly resetting = signal<ManagedUser | null>(null);
    protected resetPassword = '';
    protected readonly resetError = signal<string | null>(null);
    protected readonly resetSaving = signal(false);
    /** Shown after a reset, so the administrator can pass the password on. */
    protected readonly resetDone = signal<{ user: ManagedUser; password: string } | null>(null);

    protected onSaved(): void {
        this.editing.set(undefined);
        this.users.reload();
    }

    protected openReset(user: ManagedUser): void {
        this.resetPassword = generateTemporaryPassword();
        this.resetError.set(null);
        this.resetDone.set(null);
        this.resetting.set(user);
    }

    protected newResetPassword(): void {
        this.resetPassword = generateTemporaryPassword();
    }

    protected saveReset(): void {
        const user = this.resetting();
        if (!user) return;
        if (this.resetPassword.length < PASSWORD_MIN_LENGTH) {
            this.resetError.set(`Parola en az ${PASSWORD_MIN_LENGTH} karakter olmalıdır.`);
            return;
        }
        this.resetSaving.set(true);
        this.service.resetPassword(user.id, this.resetPassword).subscribe({
            next: () => {
                this.resetSaving.set(false);
                this.resetting.set(null);
                this.resetDone.set({ user, password: this.resetPassword });
                this.users.reload();
            },
            error: err => {
                this.resetSaving.set(false);
                this.resetError.set(fieldErrorsOf(err)['temporaryPassword']?.[0] ?? null);
            },
        });
    }
}
