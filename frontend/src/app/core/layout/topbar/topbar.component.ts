import { Component, inject } from '@angular/core';
import { AuthService, ROLE_LABELS } from '../../auth/auth.service';

@Component({
  selector: 'app-topbar',
  templateUrl: './topbar.component.html',
  styleUrl: './topbar.component.scss',
})
export class TopbarComponent {
  auth = inject(AuthService);
  roleLabels = ROLE_LABELS;
}