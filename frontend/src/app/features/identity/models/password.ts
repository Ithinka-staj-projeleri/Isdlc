/** Same rule as the backend's PasswordRules (docs/multi-tenancy.md → Parola). */
export const PASSWORD_MIN_LENGTH = 12;

/** The password change form's own check before sending; the API checks the same and more ("Mevcut parola hatalı."). */
export function checkNewPassword(current: string, next: string, repeat: string): string | null {
    if (!current || !next) return 'Mevcut ve yeni parola zorunludur.';
    if (next.length < PASSWORD_MIN_LENGTH) return `Parola en az ${PASSWORD_MIN_LENGTH} karakter olmalıdır.`;
    if (next === current) return 'Yeni parola eskisinden farklı olmalıdır.';
    if (next !== repeat) return 'Yeni parolalar aynı değil.';
    return null;
}

/**
 * A temporary password for the administrator to pass on (16 characters, no look-alikes such as 0/O, 1/l).
 * The user changes it at the first sign-in.
 */
export function generateTemporaryPassword(length = 16): string {
    const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';
    const values = new Uint32Array(length);
    crypto.getRandomValues(values);
    return Array.from(values, v => alphabet[v % alphabet.length]).join('');
}
