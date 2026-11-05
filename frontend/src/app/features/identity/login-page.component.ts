import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login-page',
  imports: [FormsModule],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.scss',
})
export class LoginPageComponent {
  private _auth = inject(AuthService);
  private _router = inject(Router);

  email = '';
  password = '';
  error = signal('');
  loading = signal(false);

  submit(): void {
    if (!this.email || !this.password) {
      this.error.set('E-posta ve şifre gerekli.');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this._auth.login(this.email, this.password).subscribe({
      next: () => this._router.navigate(['/dashboard']),
      error: (err: Error) => {
        this.loading.set(false);
        this.error.set(err.message || 'Giriş yapılamadı.');
      },
    });
  }
}