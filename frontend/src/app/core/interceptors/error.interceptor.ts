import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService, LOGIN_URL } from '../auth/auth.service';
import { problemOf, SHOWS_FIELD_ERRORS, SILENT_ERRORS } from '../http/problem-details';

/** Used when the API sends no message of its own (server down, unexpected error, ...). */
const MESSAGES: Record<number, string> = {
  0: 'Sunucuya ulaşılamıyor.',
  400: 'Gönderilen bilgiler geçersiz.',
  401: 'Oturumunuz sona erdi, lütfen tekrar giriş yapın.',
  403: 'Bu işlem için yetkiniz yok.',
  404: 'Kayıt bulunamadı.',
  409: 'Kayıt başka bir kullanıcı tarafından değiştirilmiş.',
  500: 'Sunucuda bir hata oluştu.',
};

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const problem = problemOf(err);
      const status = problem?.status ?? err.status;
      console.error('API hatası:', err.error ?? err);

      // The login page shows its own errors ("E-posta veya parola hatalı.").
      if (req.url === LOGIN_URL) {
        return throwError(() => err);
      }

      // The token has expired or is no longer valid: sign in again, then come back to this page.
      // No alert: several requests can fail at once; the login page tells the user what happened.
      if (status === 401) {
        if (!router.url.startsWith('/login')) {
          auth.logout(router.url);
        }
        return throwError(() => err);
      }

      // Background requests (the bell asking every minute) must not open an alert every time the server is down.
      if (req.context.get(SILENT_ERRORS)) {
        return throwError(() => err);
      }

      // The form shows these under its fields; an alert on top would say the same thing twice.
      const fieldErrorsShownByForm = status === 400 && !!problem?.errors && req.context.get(SHOWS_FIELD_ERRORS);

      if (!fieldErrorsShownByForm) {
        // The API's own Turkish message is more helpful than the general one (500 never carries details).
        const message = (status !== 500 && problem?.detail) || MESSAGES[status] || 'Beklenmeyen bir hata oluştu.';
        alert(message);
      }

      return throwError(() => err);
    }),
  );
};
