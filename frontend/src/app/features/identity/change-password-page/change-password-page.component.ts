import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { fieldErrorsOf, problemOf, silentErrors } from '../../../core/http/problem-details';
import { PASSWORD_MIN_LENGTH, checkNewPassword } from '../models/password';

/**
 * /change-password: required after signing in with a temporary password (docs/multi-tenancy.md → Parola).
 * Every other page and request is closed until it is changed.
 */
@Component({
  selector: 'app-change-password-page',
  imports: [FormsModule],
  templateUrl: './change-password-page.component.html',
  styleUrl: './change-password-page.component.scss',
})
export class ChangePasswordPageComponent {
  protected readonly auth = inject(AuthService);
  private readonly _router = inject(Router);

  protected readonly minLength = PASSWORD_MIN_LENGTH;
  protected readonly required = this.auth.mustChangePassword();

  currentPassword = '';
  newPassword = '';
  repeatPassword = '';
  protected readonly error = signal('');
  protected readonly saving = signal(false);

  submit(): void {
    const problem = checkNewPassword(this.currentPassword, this.newPassword, this.repeatPassword);
    this.error.set(problem ?? '');
    if (problem) {
      return;
    }

    this.saving.set(true);
    // The page shows every message itself; a 401 still signs the user out.
    this.auth.changePassword(this.currentPassword, this.newPassword, silentErrors()).subscribe({
      next: () => this._router.navigateByUrl(this.auth.homeUrl()),
      error: (err: unknown) => {
        this.saving.set(false);
        this.error.set(changePasswordErrorMessage(err));
      },
    });
  }
}

function changePasswordErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse && err.status === 0) {
    return 'Sunucuya ulaşılamıyor.';
  }
  const firstFieldError = Object.values(fieldErrorsOf(err))[0]?.[0];
  const problem = problemOf(err);
  if (firstFieldError) return firstFieldError;
  // 400 "Mevcut parola hatalı.", 403 and the like carry their own Turkish message; 500 never carries details.
  return (problem?.status !== 500 && problem?.detail) || 'Parola değiştirilemedi.';
}
