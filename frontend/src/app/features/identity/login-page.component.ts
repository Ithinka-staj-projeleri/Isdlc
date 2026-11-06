import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { fieldErrorsOf, problemOf } from '../../core/http/problem-details';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss',
})
export class LoginPageComponent {
  private _auth = inject(AuthService);
  private _router = inject(Router);

  /** Page the user tried to open (or was on when the session ended); opened again after sign-in. */
  private readonly _returnUrl = safeReturnUrl(inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl'));

  /** Shown when the user was sent here from another page (not signed in, or session ended). */
  readonly notice = this._returnUrl ? 'Devam etmek için giriş yapın.' : '';

  email = '';
  password = '';
  error = signal('');
  loading = signal(false);

  submit(): void {
    if (!this.email || !this.password) {
      this.error.set('E-posta ve parola zorunludur.');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this._auth.login(this.email, this.password).subscribe({
      next: () => this._router.navigateByUrl(this._returnUrl ?? '/dashboard'),
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(loginErrorMessage(err));
      },
    });
  }
}

function loginErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse && err.status === 0) {
    return 'Sunucuya ulaşılamıyor.';
  }
  const problem = problemOf(err);
  // 401 carries "E-posta veya parola hatalı."; 500 never carries details.
  if (problem?.detail && problem.status !== 500) {
    return problem.detail;
  }
  const firstFieldError = Object.values(fieldErrorsOf(err))[0]?.[0];
  return firstFieldError ?? 'Giriş yapılamadı.';
}

/** Only pages of this app: "/projects/5" is fine, "https://..." or "//evil.com" is not. */
function safeReturnUrl(url: string | null): string | null {
  return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/login') ? url : null;
}
