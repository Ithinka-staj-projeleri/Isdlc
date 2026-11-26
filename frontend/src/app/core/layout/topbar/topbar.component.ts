import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { GlobalSearchComponent } from '../../../features/search/global-search/global-search.component';
import { NotificationBellComponent } from '../../../features/notifications/notification-bell/notification-bell.component';

@Component({
  imports: [RouterLink, GlobalSearchComponent, NotificationBellComponent],
  selector: 'app-topbar',
  styleUrl: './topbar.component.scss',
  templateUrl: './topbar.component.html',
})
export class TopbarComponent {
  protected readonly auth = inject(AuthService);
}
