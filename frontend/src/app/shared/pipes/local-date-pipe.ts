import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'localDate' })
export class LocalDatePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';
    const iso = /[zZ]|[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z';
    return new Date(iso).toLocaleString('tr-TR');
  }
}