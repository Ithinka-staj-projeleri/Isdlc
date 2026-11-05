import { Pipe, PipeTransform } from '@angular/core';

/**
 * A date without time from the API ("2026-11-05") as "05.11.2026".
 * Read as a calendar date, so the browser's time zone never moves it to another day.
 */
@Pipe({ name: 'dateOnly' })
export class DateOnlyPipe implements PipeTransform {
    transform(value: string | null | undefined): string {
        const match = value ? /^(\d{4})-(\d{2})-(\d{2})/.exec(value) : null;
        return match ? `${match[3]}.${match[2]}.${match[1]}` : '';
    }
}
