import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { problemOf, SHOWS_FIELD_ERRORS } from '../http/problem-details';

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

export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const problem = problemOf(err);
      const status = problem?.status ?? err.status;
      console.error('API hatası:', err.error ?? err);

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
