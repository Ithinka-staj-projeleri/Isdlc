import { Component, inject } from '@angular/core';
import { AuthService } from '../../auth/auth.service';
import { USER_ROLE_LABELS } from '../../../shared/models/user-role';

@Component({
  selector: 'app-topbar',
  styleUrl: './topbar.component.scss',
  templateUrl: './topbar.component.html',
})
export class TopbarComponent {
  protected readonly auth = inject(AuthService);
  protected readonly roleLabels = USER_ROLE_LABELS;
}
