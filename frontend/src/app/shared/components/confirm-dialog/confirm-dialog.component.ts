import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.scss',
})
export class ConfirmDialogComponent {
  open = input(false);
  title = input('Onay');
  message = input('Bu işlemi onaylıyor musunuz?');
  confirmed = output<void>();
  cancelled = output<void>();
}