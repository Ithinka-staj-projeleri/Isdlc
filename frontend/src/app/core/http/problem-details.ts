import { HttpContext, HttpContextToken, HttpErrorResponse } from '@angular/common/http';

/** Error body the API sends (AGENTS.md → Ortak API kuralları). */
export interface ProblemDetails {
    title?: string;
    status?: number;
    /** Turkish message meant for the user ("Proje aktif değil; ..."). */
    detail?: string;
    traceId?: string;
    /** 400 validation errors: JSON field name → messages ({ "title": ["Konu zorunludur."] }). */
    errors?: Record<string, string[]>;
}

/**
 * Set on a request whose form shows 400 field errors under its own fields,
 * so the error interceptor does not also show an alert for them:
 * `http.post(url, body, { context: showsFieldErrors() })`
 */
export const SHOWS_FIELD_ERRORS = new HttpContextToken<boolean>(() => false);

export function showsFieldErrors(): HttpContext {
    return new HttpContext().set(SHOWS_FIELD_ERRORS, true);
}

/**
 * Set on a background request that the user did not ask for (e.g. the bell's unread count every minute):
 * a failure is only logged, no alert. A 401 still signs the user out.
 * `http.get(url, { context: silentErrors() })`
 */
export const SILENT_ERRORS = new HttpContextToken<boolean>(() => false);

export function silentErrors(): HttpContext {
    return new HttpContext().set(SILENT_ERRORS, true);
}

export function problemOf(error: unknown): ProblemDetails | undefined {
    return error instanceof HttpErrorResponse && error.error && typeof error.error === 'object'
        ? (error.error as ProblemDetails)
        : undefined;
}

/** Field errors of a 400 response; empty when there are none. */
export function fieldErrorsOf(error: unknown): Record<string, string[]> {
    return problemOf(error)?.errors ?? {};
}
