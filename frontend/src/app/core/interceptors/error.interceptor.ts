import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

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
      if (req.url.includes('/auth/login')) return throwError(() => err);
      const status = err.error?.status ?? err.status;
      const message = MESSAGES[status] ?? 'Beklenmeyen bir hata oluştu.';
      console.error('API hatası:', err.error ?? err);
      alert(message);
      return throwError(() => err);
    }),
  );